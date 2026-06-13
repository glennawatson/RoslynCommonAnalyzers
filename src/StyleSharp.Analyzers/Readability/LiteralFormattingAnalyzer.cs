// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped readability analyzer for literal formatting: digit separators in long numbers and explicit
/// escapes for control characters in strings. Both rules are opt-in.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1191 — a long base-10 integer literal has no digit separators (opt-in).</description></item>
/// <item><description>SST1192 — a string literal embeds a raw control character (opt-in).</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LiteralFormattingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The digit count at or above which a separator-free integer literal is flagged.</summary>
    private const int DigitThreshold = 5;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.UseDigitSeparators,
        ReadabilityRules.EscapeControlCharacters);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeNumericLiteral, SyntaxKind.NumericLiteralExpression);
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Returns whether a numeric literal is a long base-10 integer with no digit separators.</summary>
    /// <param name="text">The literal token text.</param>
    /// <returns><see langword="true"/> when digit separators would aid readability.</returns>
    internal static bool ShouldGroupDigits(string text)
    {
        // Only plain base-10 integers are considered; hex, binary, floating-point, and grouped literals
        // each have their own grouping conventions and are left alone.
        if (text.Length < DigitThreshold
            || text[0] == '0'
            || text.IndexOf('_') >= 0)
        {
            return false;
        }

        var digits = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is >= '0' and <= '9')
            {
                digits++;
                continue;
            }

            // The first non-digit must begin a type suffix ('L', 'U', …); anything else rules it out.
            return IsIntegerSuffix(text, i) && digits >= DigitThreshold;
        }

        return digits >= DigitThreshold;
    }

    /// <summary>Returns whether a raw string token embeds a control character written verbatim.</summary>
    /// <param name="text">The literal token text.</param>
    /// <returns><see langword="true"/> when a control character appears unescaped in the source.</returns>
    internal static bool HasRawControlCharacter(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] < ' ')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports SST1191 for a long base-10 integer literal without digit separators.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeNumericLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (!ShouldGroupDigits(literal.Token.Text))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseDigitSeparators, literal.GetLocation(), literal.Token.Text));
    }

    /// <summary>Reports SST1192 for a string literal that embeds a raw control character.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (!HasRawControlCharacter(literal.Token.Text))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.EscapeControlCharacters, literal.GetLocation()));
    }

    /// <summary>Returns whether the remainder of a numeric literal from <paramref name="start"/> is a valid integer suffix.</summary>
    /// <param name="text">The literal token text.</param>
    /// <param name="start">The index of the first suffix character.</param>
    /// <returns><see langword="true"/> for an <c>L</c>/<c>U</c>/<c>UL</c>/<c>LU</c> suffix (any case).</returns>
    private static bool IsIntegerSuffix(string text, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            var c = char.ToUpperInvariant(text[i]);
            if (c is not ('L' or 'U'))
            {
                return false;
            }
        }

        return true;
    }
}
