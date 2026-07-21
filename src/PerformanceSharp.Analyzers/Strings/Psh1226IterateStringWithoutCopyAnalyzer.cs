// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a <c>string.ToCharArray()</c> whose <c>char[]</c> is stored in a local that is then only
/// iterated (PSH1226) — a <c>foreach</c>, a <c>Length</c> read, or an index read. A string is already
/// indexable and enumerable, so the copy buys nothing; dropping the <c>ToCharArray()</c> iterates the
/// string itself.
/// </summary>
/// <remarks>
/// <para>
/// The uses are a whitelist. A local that is mutated (<c>chars[i] = 'x'</c>), reassigned, returned,
/// passed as an argument, or touched in any way a string could not stand in for is left alone by
/// construction: the moment a reference is not one of the three iteration shapes, the rule stands
/// down, so a genuine mutable-buffer use is never rewritten into code that would not compile.
/// </para>
/// <para>
/// This is the stored-array shape only — a copy the initializer hands straight to a <c>foreach</c>, a
/// <c>Length</c> read, or an indexer, without a local in between, is a different and already-reported
/// shape, so the two never both fire. The local's declared type must be <c>var</c> or <c>char[]</c>,
/// because those are the two the fix can retype to a string; any other declared type is not reported.
/// The clean path is a token check on the call; nothing binds until a <c>ToCharArray</c> is found
/// initializing a local.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1226IterateStringWithoutCopyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The copying member name on <see cref="string"/>.</summary>
    internal const string ToCharArrayMethodName = "ToCharArray";

    /// <summary>The member name of the length read a string satisfies without a copy.</summary>
    private const string LengthPropertyName = "Length";

    /// <summary>The contextual keyword marking an implicitly typed local.</summary>
    private const string VarKeyword = "var";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.IterateStringWithoutCopy);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns whether an invocation is a bare <c>x.ToCharArray()</c>, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsToCharArrayShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == ToCharArrayMethodName;

    /// <summary>Resolves the local a <c>ToCharArray</c> copy initializes, when the retypeable-local shape matches.</summary>
    /// <param name="invocation">The copying invocation.</param>
    /// <param name="declarator">The variable declarator the copy initializes.</param>
    /// <param name="localDeclaration">The local declaration statement.</param>
    /// <returns><see langword="true"/> when the copy is the sole initializer of a single <c>var</c> or <c>char[]</c> local.</returns>
    internal static bool TryGetRetypeableLocal(
        InvocationExpressionSyntax invocation,
        [NotNullWhen(true)] out VariableDeclaratorSyntax? declarator,
        [NotNullWhen(true)] out LocalDeclarationStatementSyntax? localDeclaration)
    {
        declarator = null;
        localDeclaration = null;
        if (!IsToCharArrayShape(invocation)
            || invocation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax candidate } equalsValue
            || equalsValue.Value != invocation
            || candidate.Parent is not VariableDeclarationSyntax { Variables.Count: 1 } declaration
            || declaration.Parent is not LocalDeclarationStatementSyntax local
            || !IsCharArrayOrVar(declaration.Type))
        {
            return false;
        }

        declarator = candidate;
        localDeclaration = local;
        return true;
    }

    /// <summary>Returns whether a declared type is <c>var</c> or a single-dimensional <c>char[]</c>.</summary>
    /// <param name="type">The declared type syntax.</param>
    /// <returns><see langword="true"/> when the fix can retype the local to a string.</returns>
    internal static bool IsCharArrayOrVar(TypeSyntax type)
    {
        if (type is IdentifierNameSyntax { Identifier.ValueText: VarKeyword })
        {
            return true;
        }

        return type is ArrayTypeSyntax { ElementType: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.CharKeyword }, RankSpecifiers.Count: 1 } array
            && array.RankSpecifiers[0].Rank == 1;
    }

    /// <summary>Reports PSH1226 for a stored copy whose local is only iterated.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetRetypeableLocal(invocation, out var declarator, out _))
        {
            return;
        }

        var model = context.SemanticModel;
        if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol copy
            || !IsStringToCharArray(copy)
            || model.GetDeclaredSymbol(declarator, context.CancellationToken) is not ILocalSymbol local
            || !IsOnlyIterated(model, local, declarator, context.CancellationToken))
        {
            return;
        }

        var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.IterateStringWithoutCopy,
            invocation.SyntaxTree,
            invocation.Span,
            receiver.ToString()));
    }

    /// <summary>Returns whether a bound member is the framework's parameterless <c>string.ToCharArray()</c>.</summary>
    /// <param name="copy">The bound copying method.</param>
    /// <returns><see langword="true"/> when the call is the string copy the rule knows how to drop.</returns>
    private static bool IsStringToCharArray(IMethodSymbol copy)
        => !copy.IsStatic
            && copy.Parameters.Length == 0
            && !copy.IsExtensionMethod
            && copy.ReducedFrom is null
            && copy.Name == ToCharArrayMethodName
            && copy.ContainingType.SpecialType == SpecialType.System_String;

    /// <summary>Returns whether every reference to a local, in its scope, is a read-only iteration use.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="local">The local the copy is stored in.</param>
    /// <param name="declarator">The local's declarator.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the local is used at least once and only for iteration.</returns>
    /// <remarks>
    /// The scan runs only after a <c>ToCharArray</c> was found initializing a local, so the descendant
    /// walk is off the clean path. A name check precedes every binding, so only same-named identifiers
    /// are resolved.
    /// </remarks>
    private static bool IsOnlyIterated(
        SemanticModel model,
        ILocalSymbol local,
        VariableDeclaratorSyntax declarator,
        CancellationToken cancellationToken)
    {
        if (declarator.FirstAncestorOrSelf<BlockSyntax>() is not { } scope)
        {
            return false;
        }

        var name = local.Name;
        var iterated = false;
        foreach (var node in scope.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier || identifier.Identifier.ValueText != name)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, local))
            {
                continue;
            }

            if (!IsIterationUse(identifier))
            {
                return false;
            }

            iterated = true;
        }

        return iterated;
    }

    /// <summary>Returns whether a reference to the local reads it as a sequence a string would satisfy.</summary>
    /// <param name="identifier">The reference to classify.</param>
    /// <returns><see langword="true"/> for a <c>foreach</c> source, a <c>Length</c> read, or an index read.</returns>
    private static bool IsIterationUse(IdentifierNameSyntax identifier) => identifier.Parent switch
    {
        ForEachStatementSyntax forEach => forEach.Expression == identifier,
        MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access =>
            access.Expression == identifier && access.Name.Identifier.ValueText == LengthPropertyName,
        ElementAccessExpressionSyntax elementAccess =>
            elementAccess.Expression == identifier && !IsWriteTarget(elementAccess),
        _ => false,
    };

    /// <summary>Returns whether an element access writes to, or takes a reference to, its element.</summary>
    /// <param name="elementAccess">The element access on the local.</param>
    /// <returns><see langword="true"/> when the element is not merely read.</returns>
    /// <remarks>
    /// A <c>char[]</c> element is a writable storage location; a <c>string</c> element is not. Every
    /// shape that needs the location rather than the value would stop compiling after the retype.
    /// </remarks>
    private static bool IsWriteTarget(ElementAccessExpressionSyntax elementAccess) => elementAccess.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == elementAccess,
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression } => true,
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression } => true,
        RefExpressionSyntax => true,
        ArgumentSyntax argument => argument.RefOrOutKeyword.RawKind != (int)SyntaxKind.None,
        _ => false,
    };
}
