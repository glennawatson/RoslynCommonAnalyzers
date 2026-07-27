// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a test against <c>object</c> that is really a null check (SST2019): <c>x is object</c> asks
/// whether <c>x</c> is non-null, and <c>x is not object</c> asks whether it is null.
/// </summary>
/// <remarks>
/// <para>
/// The rule stays silent when the operand is a non-nullable value type — there the test is a constant the
/// compiler already reports on — and when the operand type cannot be resolved at all.
/// </para>
/// <para>
/// <c>is not null</c> is C# 9 syntax, so the whole rule is gated on the parse options; a project on an
/// older language version pays a single version comparison and nothing else. The type test itself is
/// answered from the syntax before the semantic model is touched, so the common case where the right-hand
/// side is some other type never asks for a symbol.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2019NullCheckOverTypeCheckAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The suggestion offered for <c>x is object</c>.</summary>
    private const string NotNullSuggestion = "is not null";

    /// <summary>The suggestion offered for <c>x is not object</c>.</summary>
    private const string NullSuggestion = "is null";

    /// <summary>The language version that introduced the <c>not</c> pattern.</summary>
    private const int CSharp9 = 900;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.NullCheckOverTypeCheck);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeIsExpression, SyntaxKind.IsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIsPattern, SyntaxKind.IsPatternExpression);
    }

    /// <summary>Reports <c>x is object</c>, which is true for exactly the non-null values.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIsExpression(SyntaxNodeAnalysisContext context)
    {
        var expression = (BinaryExpressionSyntax)context.Node;
        if (!IsObjectType(expression.Right) || !IsLanguageSupported(expression))
        {
            return;
        }

        Report(context, expression.Left, expression.GetLocation(), NotNullSuggestion);
    }

    /// <summary>Reports <c>x is not object</c>, which is true for exactly the null values.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIsPattern(SyntaxNodeAnalysisContext context)
    {
        var expression = (IsPatternExpressionSyntax)context.Node;
        if (expression.Pattern is not UnaryPatternSyntax { Pattern: TypePatternSyntax typePattern } unary
            || !unary.OperatorToken.IsKind(SyntaxKind.NotKeyword)
            || !IsObjectType(typePattern.Type)
            || !IsLanguageSupported(expression))
        {
            return;
        }

        Report(context, expression.Expression, expression.GetLocation(), NullSuggestion);
    }

    /// <summary>Reports the test when its operand is something that can actually be null.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="operand">The tested expression.</param>
    /// <param name="location">The location to report.</param>
    /// <param name="suggestion">The replacement spelling to name in the message.</param>
    private static void Report(SyntaxNodeAnalysisContext context, ExpressionSyntax operand, Location location, string suggestion)
    {
        var type = context.SemanticModel.GetTypeInfo(operand, context.CancellationToken).Type;
        if (type is null || IsNonNullableValueType(type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernizationRules.NullCheckOverTypeCheck, location, suggestion));
    }

    /// <summary>Returns whether a type syntax names <c>object</c>, before any symbol is resolved.</summary>
    /// <param name="type">The syntax on the right of the test, which the parser types as an expression.</param>
    /// <returns><see langword="true"/> for the <c>object</c> keyword.</returns>
    /// <remarks>
    /// Only the keyword spelling counts. <c>System.Object</c> written out, or an alias for it, is rare
    /// enough that resolving every right-hand side to catch it would cost more than it saves.
    /// </remarks>
    private static bool IsObjectType(ExpressionSyntax type)
        => type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword);

    /// <summary>Returns whether the operand is a value type that has no null to test for.</summary>
    /// <param name="type">The operand's type.</param>
    /// <returns><see langword="true"/> when the type is a non-nullable value type.</returns>
    private static bool IsNonNullableValueType(ITypeSymbol type)
        => type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;

    /// <summary>Returns whether the tree is parsed at a language version that has the <c>not</c> pattern.</summary>
    /// <param name="node">A node in the syntax tree.</param>
    /// <returns><see langword="true"/> for C# 9 or later.</returns>
    private static bool IsLanguageSupported(SyntaxNode node)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= CSharp9;
}
