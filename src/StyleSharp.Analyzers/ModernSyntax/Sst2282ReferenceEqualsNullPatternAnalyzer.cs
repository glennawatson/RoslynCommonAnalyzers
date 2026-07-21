// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a call to <c>object.ReferenceEquals</c> that compares a value against the <c>null</c> literal
/// (SST2282). <c>ReferenceEquals(value, null)</c> — in either argument order — states a null check as a
/// static call, and <c>value is null</c> (with <c>!ReferenceEquals(value, null)</c> as
/// <c>value is not null</c>) says the same thing as a pattern that needs no receiver. Reported only when
/// the call binds to <see cref="object.ReferenceEquals(object, object)"/> and the non-null operand is a
/// reference type or an unconstrained type parameter, where <c>is null</c> is legal; a value-type operand
/// is left alone because <c>ReferenceEquals</c> boxes it, a separate concern.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2282ReferenceEqualsNullPatternAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The message text for the plain <c>is null</c> rewrite.</summary>
    internal const string IsNullText = "is null";

    /// <summary>The message text for the negated <c>is not null</c> rewrite.</summary>
    internal const string IsNotNullText = "is not null";

    /// <summary>The <c>object.ReferenceEquals</c> method name.</summary>
    private const string ReferenceEqualsName = "ReferenceEquals";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArrays.Of(ModernSyntaxRules.UseNullPatternOverReferenceEquals);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    /// <summary>
    /// Gets the operand that is not the <c>null</c> literal from a two-argument, <c>ReferenceEquals</c>-named
    /// call. Purely syntactic, so the analyzer and the code fix re-derive the same operand.
    /// </summary>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="nonNullOperand">The operand compared against <c>null</c> when matched.</param>
    /// <returns><see langword="true"/> when exactly one argument is the <c>null</c> literal.</returns>
    internal static bool TryGetNonNullOperand(InvocationExpressionSyntax invocation, out ExpressionSyntax nonNullOperand)
    {
        nonNullOperand = null!;
        if (!IsReferenceEqualsName(invocation.Expression) || invocation.ArgumentList.Arguments.Count != 2)
        {
            return false;
        }

        var first = invocation.ArgumentList.Arguments[0].Expression;
        var second = invocation.ArgumentList.Arguments[1].Expression;
        var firstNull = ExpressionSimplificationAnalyzer.Unwrap(first).IsKind(SyntaxKind.NullLiteralExpression);
        var secondNull = ExpressionSimplificationAnalyzer.Unwrap(second).IsKind(SyntaxKind.NullLiteralExpression);

        // Exactly one operand must be null: 'ReferenceEquals(null, null)' has no value to test and a call
        // with neither operand null is not a null check.
        if (firstNull == secondNull)
        {
            return false;
        }

        nonNullOperand = firstNull ? second : first;
        return true;
    }

    /// <summary>Gets the <c>!</c> that directly negates the call, when one encloses it (through parentheses).</summary>
    /// <param name="invocation">The <c>ReferenceEquals</c> invocation.</param>
    /// <returns>The enclosing logical-not expression, or <see langword="null"/> when the call is not negated.</returns>
    internal static PrefixUnaryExpressionSyntax? GetEnclosingLogicalNot(InvocationExpressionSyntax invocation)
    {
        ExpressionSyntax current = invocation;
        var parent = invocation.Parent;
        while (parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
            parent = parenthesized.Parent;
        }

        return parent is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not && not.Operand == current
            ? not
            : null;
    }

    /// <summary>Reports a <c>ReferenceEquals</c> null check that a direct null pattern expresses.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // A syntactic prepass rejects every non-candidate before any binding runs.
        if (!TryGetNonNullOperand(invocation, out var nonNullOperand))
        {
            return;
        }

        var negated = GetEnclosingLogicalNot(invocation) is not null;
        if (!SupportsRequiredPattern(invocation, negated))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            {
                IsStatic: true,
                Name: ReferenceEqualsName,
                ContainingType.SpecialType: SpecialType.System_Object,
            })
        {
            return;
        }

        // 'is null' is legal for a reference type or an unconstrained type parameter; a value-type operand is
        // boxed by 'ReferenceEquals', so rewriting the shape would change what runs and is left alone.
        var type = context.SemanticModel.GetTypeInfo(nonNullOperand, context.CancellationToken).Type;
        if (type is not { IsValueType: false })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UseNullPatternOverReferenceEquals,
            invocation.GetLocation(),
            negated ? IsNotNullText : IsNullText));
    }

    /// <summary>Returns whether an expression names <c>ReferenceEquals</c> as an identifier or member access.</summary>
    /// <param name="expression">The invoked expression.</param>
    /// <returns><see langword="true"/> for a <c>ReferenceEquals</c> name in either spelling.</returns>
    private static bool IsReferenceEqualsName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == ReferenceEqualsName,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText == ReferenceEqualsName,
        _ => false,
    };

    /// <summary>Returns whether the tree's language version supports the pattern the rewrite needs.</summary>
    /// <param name="node">A node in the tree.</param>
    /// <param name="negated">Whether the rewrite is the negated <c>is not null</c> form.</param>
    /// <returns><see langword="true"/> when the constant null pattern (C# 7) — or the <c>not</c> pattern (C# 9) when negated — is available.</returns>
    private static bool SupportsRequiredPattern(SyntaxNode node, bool negated)
        => node.SyntaxTree.Options is CSharpParseOptions options
            && options.LanguageVersion >= (negated ? LanguageVersion.CSharp9 : LanguageVersion.CSharp7);
}
