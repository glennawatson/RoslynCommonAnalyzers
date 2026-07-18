// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports two concatenated string literals whose seam fuses a SQL keyword because the space between them is
/// missing (SST2470). <c>"SELECT * FROM t" + "WHERE id = 1"</c> runs as <c>"...tWHERE..."</c> and
/// <c>"...WHERE a = 1" + "AND b = 2"</c> runs as <c>"...1AND..."</c>: the keyword is swallowed into the adjacent
/// token, so the query the database receives is not the one written.
/// </summary>
/// <remarks>
/// <para>
/// Both operands of the <c>+</c> must be string literals — regular, verbatim, or raw — read straight off the
/// syntax tree. Nothing is bound, and an interpolated string, a named constant, or any other runtime value on
/// either side is never a candidate, because the fused text can only be known when both halves are literal.
/// </para>
/// <para>
/// Two seam shapes are reported, and both require the fusion to actually merge tokens — the character on each
/// side of the seam must be a word character (an ASCII letter, digit, or underscore), since only then do the two
/// tokens run together. The first shape: the left literal ends in a word character and the right literal begins
/// with a curated SQL keyword (<c>WHERE</c>, <c>AND</c>, <c>OR</c>, <c>FROM</c>, <c>JOIN</c>, <c>INNER</c>,
/// <c>LEFT</c>, <c>RIGHT</c>, <c>ON</c>, <c>ORDER</c>, <c>GROUP</c>, <c>HAVING</c>, <c>SELECT</c>, <c>SET</c>,
/// <c>VALUES</c>, <c>INTO</c>, <c>UNION</c>, <c>LIMIT</c>, <c>OFFSET</c>, <c>RETURNING</c>, case-insensitive). The
/// symmetric shape: the left literal ends with one of those keywords and the right literal begins with a word
/// character. In both, the keyword must sit on a token boundary — a non-word character or the string end must
/// bound it on the far side — so <c>"WHEREVER"</c> is not treated as a leading <c>WHERE</c>.
/// </para>
/// <para>
/// To keep ordinary prose concatenations quiet the rule adds a conservative gate: the left literal must itself
/// read as SQL by containing a strong SQL keyword as a delimited token (<c>SELECT</c>, <c>FROM</c>, <c>WHERE</c>,
/// <c>JOIN</c>, <c>INTO</c>, <c>VALUES</c>, <c>UNION</c>, <c>HAVING</c>, <c>RETURNING</c>, <c>INSERT</c>,
/// <c>UPDATE</c>, <c>DELETE</c>). This drops false positives such as <c>"click here" + "OR press escape"</c>,
/// whose right side starts with the weak keyword <c>OR</c> but whose left side is not SQL. The narrow keyword
/// set for weak seam words paired with the strong-keyword gate on the left is what keeps the rule near
/// zero false positives.
/// </para>
/// <para>
/// Only a direct pair of adjacent string literals is inspected; a keyword fused across the outer seam of a
/// longer concatenation chain (where one side is itself a concatenation) is not reported. The clean path is
/// purely syntactic: a <c>+</c> whose operands are not both string literals returns at once, and the keyword
/// scans run only after the cheap seam check holds, so no work and no allocation happen until a real fusion is
/// found.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2470FusedSqlKeywordAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The SQL keywords a fused seam is scanned for on the right start or the left end.</summary>
    private static readonly string[] SeamKeywords =
    [
        "WHERE", "AND", "OR", "FROM", "JOIN", "INNER", "LEFT", "RIGHT", "ON", "ORDER",
        "GROUP", "HAVING", "SELECT", "SET", "VALUES", "INTO", "UNION", "LIMIT", "OFFSET", "RETURNING",
    ];

    /// <summary>The strong SQL keywords whose presence marks the left literal as SQL rather than prose.</summary>
    private static readonly string[] StrongSqlKeywords =
    [
        "SELECT", "FROM", "WHERE", "JOIN", "INTO", "VALUES", "UNION", "HAVING", "RETURNING", "INSERT", "UPDATE", "DELETE",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.FusedSqlKeyword);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AddExpression);
    }

    /// <summary>Returns the fused seam token for a literal pair, or <see langword="null"/> when nothing is fused.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The fused token preview (for example <c>tWHERE</c>), or <see langword="null"/> when the pair is clean.</returns>
    internal static string? TryGetFusedSeam(ExpressionSyntax left, ExpressionSyntax right)
    {
        if (!TryGetLiteralText(left, out var leftText) || !TryGetLiteralText(right, out var rightText))
        {
            return null;
        }

        if (leftText.Length == 0 || rightText.Length == 0)
        {
            return null;
        }

        if (!IsFusedSeam(leftText, rightText) || !ContainsStrongKeyword(leftText))
        {
            return null;
        }

        return BuildSeamPreview(leftText, rightText);
    }

    /// <summary>Analyzes one add expression for a fused SQL keyword seam.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (TryGetFusedSeam(binary.Left, binary.Right) is not { } seam)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.FusedSqlKeyword, binary.GetLocation(), seam));
    }

    /// <summary>Returns the decoded value of a string-literal operand, or <see langword="false"/> for anything else.</summary>
    /// <param name="expression">The operand to inspect.</param>
    /// <param name="text">The decoded literal value when the operand is a string literal.</param>
    /// <returns><see langword="true"/> when the operand is a regular, verbatim, or raw string literal.</returns>
    private static bool TryGetLiteralText(ExpressionSyntax expression, out string text)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            text = literal.Token.ValueText;
            return true;
        }

        text = string.Empty;
        return false;
    }

    /// <summary>Returns whether the seam between two non-empty literal values fuses a SQL keyword.</summary>
    /// <param name="leftText">The left literal's decoded value.</param>
    /// <param name="rightText">The right literal's decoded value.</param>
    /// <returns><see langword="true"/> when a keyword runs into the adjacent token.</returns>
    private static bool IsFusedSeam(string leftText, string rightText)
        => (IsWordChar(leftText[leftText.Length - 1]) && StartsWithSeamKeyword(rightText))
        || (IsWordChar(rightText[0]) && EndsWithSeamKeyword(leftText));

    /// <summary>Returns whether text begins with a seam keyword bounded by a token boundary on its far side.</summary>
    /// <param name="text">The right literal's decoded value.</param>
    /// <returns><see langword="true"/> when a leading seam keyword is present.</returns>
    private static bool StartsWithSeamKeyword(string text)
    {
        for (var i = 0; i < SeamKeywords.Length; i++)
        {
            var keyword = SeamKeywords[i];
            if (text.Length >= keyword.Length
                && string.Compare(text, 0, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (text.Length == keyword.Length || !IsWordChar(text[keyword.Length])))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether text ends with a seam keyword bounded by a token boundary on its near side.</summary>
    /// <param name="text">The left literal's decoded value.</param>
    /// <returns><see langword="true"/> when a trailing seam keyword is present.</returns>
    private static bool EndsWithSeamKeyword(string text)
    {
        for (var i = 0; i < SeamKeywords.Length; i++)
        {
            var keyword = SeamKeywords[i];
            if (text.Length < keyword.Length)
            {
                continue;
            }

            var start = text.Length - keyword.Length;
            if (string.Compare(text, start, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (start == 0 || !IsWordChar(text[start - 1])))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether text contains a strong SQL keyword as a delimited token.</summary>
    /// <param name="text">The left literal's decoded value.</param>
    /// <returns><see langword="true"/> when the left literal reads as SQL.</returns>
    private static bool ContainsStrongKeyword(string text)
    {
        for (var i = 0; i < StrongSqlKeywords.Length; i++)
        {
            var keyword = StrongSqlKeywords[i];
            var index = 0;
            while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var end = index + keyword.Length;
                var leftBounded = index == 0 || !IsWordChar(text[index - 1]);
                var rightBounded = end == text.Length || !IsWordChar(text[end]);
                if (leftBounded && rightBounded)
                {
                    return true;
                }

                index = end;
            }
        }

        return false;
    }

    /// <summary>Builds a short preview of the fused token at the seam.</summary>
    /// <param name="leftText">The left literal's decoded value.</param>
    /// <param name="rightText">The right literal's decoded value.</param>
    /// <returns>The trailing word run of the left joined to the leading word run of the right.</returns>
    private static string BuildSeamPreview(string leftText, string rightText)
    {
        var leftStart = leftText.Length;
        while (leftStart > 0 && IsWordChar(leftText[leftStart - 1]))
        {
            leftStart--;
        }

        var rightEnd = 0;
        while (rightEnd < rightText.Length && IsWordChar(rightText[rightEnd]))
        {
            rightEnd++;
        }

        return leftText.Substring(leftStart) + rightText.Substring(0, rightEnd);
    }

    /// <summary>Returns whether a character is an ASCII word character (letter, digit, or underscore).</summary>
    /// <param name="value">The character to classify.</param>
    /// <returns><see langword="true"/> when the character can run into an adjacent token.</returns>
    private static bool IsWordChar(char value)
        => value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
}
