// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a numeric literal that is cast to a wider or floating type when a literal suffix would
/// express the type directly (SST1139) — <c>(long)1</c> instead of <c>1L</c>. The suffix is shorter
/// and avoids a runtime-looking cast on a compile-time constant.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseLiteralSuffixAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.UseLiteralSuffix);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CastExpression);
    }

    /// <summary>Returns the literal suffix for a cast to a predefined numeric type, or <see langword="null"/>.</summary>
    /// <param name="cast">The cast expression.</param>
    /// <returns>The suffix (for example <c>L</c>), or <see langword="null"/> when the rule does not apply.</returns>
    internal static string? SuffixFor(CastExpressionSyntax cast)
    {
        if (cast.Type is not PredefinedTypeSyntax predefined
            || Unwrap(cast.Expression) is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            return null;
        }

        var text = literal.Token.Text;
        if (text.Length == 0 || char.IsLetter(text[text.Length - 1]))
        {
            return null;
        }

        return SuffixForKeyword(predefined.Keyword.Kind(), HasFloatingForm(text));
    }

    /// <summary>Removes a single layer of parentheses from an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The inner expression when parenthesized, otherwise the expression itself.</returns>
    internal static ExpressionSyntax Unwrap(ExpressionSyntax expression)
        => expression is ParenthesizedExpressionSyntax parenthesized ? parenthesized.Expression : expression;

    /// <summary>Returns the literal suffix for a predefined numeric keyword and literal form.</summary>
    /// <param name="keyword">The cast's predefined-type keyword kind.</param>
    /// <param name="floating">Whether the literal is written in floating-point form.</param>
    /// <returns>The suffix, or <see langword="null"/> when no suffix applies.</returns>
    private static string? SuffixForKeyword(SyntaxKind keyword, bool floating) => keyword switch
    {
        SyntaxKind.LongKeyword when !floating => "L",
        SyntaxKind.ULongKeyword when !floating => "UL",
        SyntaxKind.UIntKeyword when !floating => "U",
        SyntaxKind.FloatKeyword => "F",
        SyntaxKind.DoubleKeyword => "D",
        SyntaxKind.DecimalKeyword => "M",
        _ => null,
    };

    /// <summary>Reports a numeric literal cast that a suffix could replace.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var cast = (CastExpressionSyntax)context.Node;
        if (SuffixFor(cast) is not { } suffix)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseLiteralSuffix, cast.GetLocation(), suffix));
    }

    /// <summary>Returns whether a numeric literal is written in floating-point form (decimal point or exponent).</summary>
    /// <param name="text">The literal token text.</param>
    /// <returns><see langword="true"/> when the literal has a decimal point or exponent.</returns>
    private static bool HasFloatingForm(string text)
    {
        if (text.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase) || text.StartsWith("0b", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return text.IndexOf('.') >= 0 || text.IndexOf('e') >= 0 || text.IndexOf('E') >= 0;
    }
}
