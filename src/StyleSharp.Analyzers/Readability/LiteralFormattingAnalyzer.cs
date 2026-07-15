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
/// <item><description>SST1119 — a numeric literal's digit separators group its digits irregularly.</description></item>
/// <item><description>SST1191 — a long base-10 integer literal has no digit separators (opt-in).</description></item>
/// <item><description>SST1192 — a string literal embeds a raw control character (opt-in).</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LiteralFormattingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The digit count at or above which a separator-free integer literal is flagged.</summary>
    private const int DigitThreshold = 5;

    /// <summary>The number of leading quotes that open a raw string literal.</summary>
    private const int RawStringQuoteRun = 3;

    /// <summary>The conventional group width for a base-10 literal.</summary>
    private const int DecimalGroupWidth = 3;

    /// <summary>A conventional group width for a hexadecimal or binary literal.</summary>
    private const int WideGroupWidth = 4;

    /// <summary>The alternate conventional group width for a hexadecimal or binary literal.</summary>
    private const int NarrowGroupWidth = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.IrregularDigitGrouping,
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
        var text = literal.Token.Text;

        // The two numeric rules are disjoint: SST1191 fires on a long literal with no separators, SST1119 on
        // a literal whose separators group irregularly, so a literal can match at most one.
        if (ShouldGroupDigits(text))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseDigitSeparators, literal.GetLocation(), text));
        }
        else if (HasIrregularGrouping(text))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.IrregularDigitGrouping, literal.GetLocation(), text));
        }
    }

    /// <summary>Returns whether a numeric literal's digit separators group its digits irregularly.</summary>
    /// <param name="text">The literal token text.</param>
    /// <returns><see langword="true"/> when a group after the first is not the base's conventional width.</returns>
    /// <remarks>
    /// The leading group may be short. Groups after it must all share one width, and that width must be the
    /// base's convention: three for decimal, four or two for hexadecimal and binary. Floating-point literals
    /// are left alone. Nothing is allocated: the group widths are counted in a single pass over the text.
    /// </remarks>
    private static bool HasIrregularGrouping(string text)
    {
        if (text.IndexOf('_') < 0)
        {
            return false;
        }

        var start = 0;
        var wide = false;
        if (text.Length > NarrowGroupWidth && text[0] == '0' && (text[1] is 'x' or 'X' or 'b' or 'B'))
        {
            start = NarrowGroupWidth;
            wide = true;
        }

        var end = FindDigitsEnd(text, start, wide);
        if (end < 0)
        {
            return false;
        }

        return IsGroupingIrregular(text, start, end, wide);
    }

    /// <summary>Finds where a literal's digits end, rejecting floating-point literals.</summary>
    /// <param name="text">The literal token text.</param>
    /// <param name="start">The first digit index.</param>
    /// <param name="wide">Whether the literal is hexadecimal or binary.</param>
    /// <returns>The index after the last digit, or <c>-1</c> for a floating-point literal.</returns>
    private static int FindDigitsEnd(string text, int start, bool wide)
    {
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '_' || IsBaseDigit(c, wide))
            {
                continue;
            }

            // A decimal literal with a fractional part or an exponent is not an integer grouping question.
            return !wide && c is '.' or 'e' or 'E' ? -1 : i;
        }

        return text.Length;
    }

    /// <summary>Returns whether a numeric literal's group widths break the base's convention.</summary>
    /// <param name="text">The literal token text.</param>
    /// <param name="start">The first digit index.</param>
    /// <param name="end">The index after the last digit.</param>
    /// <param name="wide">Whether the literal is hexadecimal or binary.</param>
    /// <returns><see langword="true"/> when a non-leading group is the wrong width.</returns>
    private static bool IsGroupingIrregular(string text, int start, int end, bool wide)
    {
        var groupIndex = 0;
        var length = 0;
        var expected = -1;
        for (var i = start; i < end; i++)
        {
            if (text[i] != '_')
            {
                length++;
                continue;
            }

            if (groupIndex >= 1 && ClosesIrregularly(length, ref expected))
            {
                return true;
            }

            groupIndex++;
            length = 0;
        }

        if (groupIndex >= 1 && ClosesIrregularly(length, ref expected))
        {
            return true;
        }

        return expected >= 0 && !IsConventionWidth(expected, wide);
    }

    /// <summary>Records a non-leading group's width and returns whether it breaks uniformity.</summary>
    /// <param name="length">The group's width.</param>
    /// <param name="expected">The width the non-leading groups must share.</param>
    /// <returns><see langword="true"/> when the group's width differs from its siblings'.</returns>
    private static bool ClosesIrregularly(int length, ref int expected)
    {
        if (expected < 0)
        {
            expected = length;
            return false;
        }

        return length != expected;
    }

    /// <summary>Returns whether a group width is a conventional one for the base.</summary>
    /// <param name="width">The group width.</param>
    /// <param name="wide">Whether the literal is hexadecimal or binary.</param>
    /// <returns><see langword="true"/> for three (decimal) or four/two (hexadecimal, binary).</returns>
    private static bool IsConventionWidth(int width, bool wide)
        => wide ? width is WideGroupWidth or NarrowGroupWidth : width == DecimalGroupWidth;

    /// <summary>Returns whether a character is a digit of the literal's base.</summary>
    /// <param name="c">The character.</param>
    /// <param name="wide">Whether the literal is hexadecimal or binary.</param>
    /// <returns><see langword="true"/> for a base digit.</returns>
    private static bool IsBaseDigit(char c, bool wide)
        => c is >= '0' and <= '9' || (wide && (c is >= 'a' and <= 'f' || c is >= 'A' and <= 'F'));

    /// <summary>Reports SST1192 for a string literal that embeds a raw control character.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var text = literal.Token.Text;

        // Only a regular string literal supports escape sequences, so it is the only place the suggested
        // fix applies. Verbatim ('@"…"') and raw ('"""…"""') literals hold their content verbatim — the
        // newlines of a multi-line raw or verbatim string, and any tabs, are intentional, not escapable.
        if (text.Length == 0 || text[0] == '@' || IsRawStringLiteral(text) || !HasRawControlCharacter(text))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.EscapeControlCharacters, literal.GetLocation()));
    }

    /// <summary>Returns whether a literal's text begins a raw string literal (three or more quotes).</summary>
    /// <param name="text">The literal token text.</param>
    /// <returns><see langword="true"/> for a raw string literal.</returns>
    private static bool IsRawStringLiteral(string text)
        => text.Length >= RawStringQuoteRun && text[0] == '"' && text[1] == '"' && text[RawStringQuoteRun - 1] == '"';

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
