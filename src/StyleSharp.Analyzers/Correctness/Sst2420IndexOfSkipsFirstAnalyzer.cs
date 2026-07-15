// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an index-of result tested with <c>&gt; 0</c> (SST2420), which treats a match at the first
/// position as "not found" because the search returns 0 for the first element and -1 for no match.
/// </summary>
/// <remarks>
/// The clean path is a syntax test: register on <c>&gt;</c> / <c>&lt;</c>, and bail unless one side is the
/// literal <c>0</c> and the other an <c>IndexOf</c> / <c>LastIndexOf</c> call. Only then is the method bound
/// to confirm it is a real index search returning <see cref="int"/> on a string, an array, a span, or a
/// list. The <c>&gt;= 1</c> form is not registered here, so a deliberate "beyond the first position" test is
/// never touched.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2420IndexOfSkipsFirstAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the generic list interface.</summary>
    private const string ListInterfaceMetadataName = "System.Collections.Generic.IList`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.IndexOfSkipsFirst);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(start =>
        {
            var listInterface = start.Compilation.GetTypeByMetadataName(ListInterfaceMetadataName);
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, listInterface), SyntaxKind.GreaterThanExpression, SyntaxKind.LessThanExpression);
        });
    }

    /// <summary>Reports one index-of comparison that skips the first position.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="listInterface">The resolved <c>IList&lt;T&gt;</c> definition, if any.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? listInterface)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (GetIndexOfCall(comparison) is not { } invocation)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.ReturnType.SpecialType != SpecialType.System_Int32
            || !IsIndexSearch(method, listInterface))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.IndexOfSkipsFirst, comparison.GetLocation(), method.Name));
    }

    /// <summary>Gets the index-of call of a <c>&gt; 0</c> / <c>0 &lt;</c> comparison, if that is its shape.</summary>
    /// <param name="comparison">The relational comparison.</param>
    /// <returns>The index-of invocation, or <see langword="null"/>.</returns>
    private static InvocationExpressionSyntax? GetIndexOfCall(BinaryExpressionSyntax comparison)
    {
        // 'IndexOf(...) > 0' — the invocation is on the left; '0 < IndexOf(...)' — it is on the right.
        return comparison.IsKind(SyntaxKind.GreaterThanExpression)
            ? Pair(comparison.Left, comparison.Right)
            : Pair(comparison.Right, comparison.Left);
    }

    /// <summary>Pairs a candidate invocation with the zero it is compared against.</summary>
    /// <param name="invocationSide">The side expected to be the invocation.</param>
    /// <param name="zeroSide">The side expected to be <c>0</c>.</param>
    /// <returns>The invocation when the shape matches, otherwise <see langword="null"/>.</returns>
    private static InvocationExpressionSyntax? Pair(ExpressionSyntax invocationSide, ExpressionSyntax zeroSide)
        => IsZero(zeroSide)
            && invocationSide is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation
            && member.Name.Identifier.ValueText is "IndexOf" or "LastIndexOf"
                ? invocation
                : null;

    /// <summary>Returns whether an expression is the literal <c>0</c>.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns><see langword="true"/> for the constant <c>0</c>.</returns>
    private static bool IsZero(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { Token.Value: int and 0 };

    /// <summary>Returns whether a method is a recognised index search on a container.</summary>
    /// <param name="method">The resolved method.</param>
    /// <param name="listInterface">The resolved <c>IList&lt;T&gt;</c> definition, if any.</param>
    /// <returns><see langword="true"/> for a string, array, span, or list search.</returns>
    private static bool IsIndexSearch(IMethodSymbol method, INamedTypeSymbol? listInterface)
    {
        var container = method.ContainingType;
        if (container is null)
        {
            return false;
        }

        if (container.SpecialType == SpecialType.System_String
            || container.Name is "Array" or "MemoryExtensions" or "ImmutableArray")
        {
            return true;
        }

        if (listInterface is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(container.OriginalDefinition, listInterface))
        {
            return true;
        }

        var interfaces = container.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i].OriginalDefinition, listInterface))
            {
                return true;
            }
        }

        return false;
    }
}
