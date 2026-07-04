// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags the copy-mutate-rebuild string transformation (PSH1209): a local initialized from
/// <c>ToCharArray()</c>, written through its indexer, and passed to <c>new string(chars)</c>
/// later in the same block allocates two throwaway buffers, where <c>string.Create</c>
/// allocates the final string once and exposes its buffer as a writable span. All three parts
/// are matched syntactically in one token scan of the enclosing block before the receiver is
/// bound. Gated on <c>string.Create</c> and <c>SpanAction</c> existing in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1209StringCreateAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string ToCharArrayMethodName = "ToCharArray";

    /// <summary>The metadata name of the span action delegate string.Create requires.</summary>
    private const string SpanActionMetadataName = "System.Buffers.SpanAction`2";

    /// <summary>The string.Create member name.</summary>
    private const string CreateMethodName = "Create";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseStringCreate);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(SpanActionMetadataName) is null
                || start.Compilation.GetSpecialType(SpecialType.System_String).GetMembers(CreateMethodName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns the buffer declarator and enclosing block of a <c>var x = s.ToCharArray();</c> statement.</summary>
    /// <param name="invocation">The candidate ToCharArray invocation.</param>
    /// <returns>The declarator and block, or <see langword="null"/> when the shape does not match.</returns>
    private static (VariableDeclaratorSyntax Declarator, BlockSyntax Block)? TryGetBufferDeclaration(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != ToCharArrayMethodName
            || invocation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
            || declarator.Parent?.Parent is not LocalDeclarationStatementSyntax { Parent: BlockSyntax block })
        {
            return null;
        }

        return (declarator, block);
    }

    /// <summary>Reports PSH1209 for a copied char buffer that is mutated and rebuilt into a string.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetBufferDeclaration(invocation) is not { } buffer)
        {
            return;
        }

        var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
        var scan = new BufferUsageScan(buffer.Declarator.Identifier.ValueText, buffer.Declarator.Identifier.SpanStart);
        DescendantTraversalHelper.VisitDescendantTokens(buffer.Block, ref scan, static (in SyntaxToken token, ref BufferUsageScan state) => state.Visit(in token));
        if (!scan.Wrote
            || !scan.Rebuilt
            || context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type?.SpecialType != SpecialType.System_String)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseStringCreate,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Token-visitor state that finds indexer writes and string rebuilds of one buffer local.</summary>
    private sealed class BufferUsageScan
    {
        /// <summary>The buffer local's name.</summary>
        private readonly string _name;

        /// <summary>The declarator identifier's position; only later tokens count.</summary>
        private readonly int _declaratorStart;

        /// <summary>Initializes a new instance of the <see cref="BufferUsageScan"/> class.</summary>
        /// <param name="name">The buffer local's name.</param>
        /// <param name="declaratorStart">The declarator identifier's position.</param>
        public BufferUsageScan(string name, int declaratorStart)
        {
            _name = name;
            _declaratorStart = declaratorStart;
        }

        /// <summary>Gets a value indicating whether an element of the buffer was assigned.</summary>
        public bool Wrote { get; private set; }

        /// <summary>Gets a value indicating whether the buffer was passed to a string constructor.</summary>
        public bool Rebuilt { get; private set; }

        /// <summary>Classifies one token; stops the walk once both parts are found.</summary>
        /// <param name="token">The token to inspect.</param>
        /// <returns><see langword="true"/> to keep walking.</returns>
        public bool Visit(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)
                || token.SpanStart <= _declaratorStart
                || token.ValueText != _name
                || token.Parent is not IdentifierNameSyntax identifier)
            {
                return true;
            }

            if (IsElementWrite(identifier))
            {
                Wrote = true;
            }
            else if (IsStringConstructorArgument(identifier))
            {
                Rebuilt = true;
            }

            return !(Wrote && Rebuilt);
        }

        /// <summary>Returns whether an identifier occurrence is the target of an element write.</summary>
        /// <param name="identifier">The identifier occurrence.</param>
        /// <returns><see langword="true"/> when the buffer's element is assigned.</returns>
        private static bool IsElementWrite(IdentifierNameSyntax identifier)
            => identifier.Parent is ElementAccessExpressionSyntax element
                && element.Expression == identifier
                && element.Parent is AssignmentExpressionSyntax assignment
                && assignment.Left == element;

        /// <summary>Returns whether an identifier occurrence is the single argument of <c>new string(...)</c>.</summary>
        /// <param name="identifier">The identifier occurrence.</param>
        /// <returns><see langword="true"/> when the buffer rebuilds a string.</returns>
        private static bool IsStringConstructorArgument(IdentifierNameSyntax identifier)
            => identifier.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Arguments.Count: 1, Parent: ObjectCreationExpressionSyntax creation } }
                && creation.Type is PredefinedTypeSyntax predefined
                && predefined.Keyword.IsKind(SyntaxKind.StringKeyword);
    }
}
