// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Regroups a numeric literal's digits canonically (SST1119): threes for decimal, fours for hexadecimal and
/// binary, counted from the right so the leading group absorbs the remainder.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1119IrregularDigitGroupingCodeFixProvider))]
[Shared]
public sealed class Sst1119IrregularDigitGroupingCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The conventional decimal group width.</summary>
    private const int DecimalGroupWidth = 3;

    /// <summary>The conventional hexadecimal and binary group width.</summary>
    private const int WideGroupWidth = 4;

    /// <summary>The length of a base prefix (<c>0x</c> or <c>0b</c>).</summary>
    private const int PrefixLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.IrregularDigitGrouping.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Group the digits evenly",
            nameof(Sst1119IrregularDigitGroupingCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported literal and rebuilds it with even grouping.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            return null;
        }

        var regrouped = Regroup(literal.Token.Text);
        var token = SyntaxFactory.ParseToken(regrouped).WithTriviaFrom(literal.Token);
        return new NodeReplacement(literal, literal.WithToken(token));
    }

    /// <summary>Rebuilds a numeric literal's text with canonical digit grouping.</summary>
    /// <param name="text">The literal token text.</param>
    /// <returns>The regrouped text.</returns>
    private static string Regroup(string text)
    {
        var start = 0;
        var wide = false;
        if (text.Length > 1 && text[0] == '0' && (text[1] is 'x' or 'X' or 'b' or 'B'))
        {
            start = PrefixLength;
            wide = true;
        }

        var suffixStart = SuffixStart(text, start);
        var width = wide ? WideGroupWidth : DecimalGroupWidth;
        var digits = Compact(text, start, suffixStart);

        var builder = new StringBuilder(text.Length);
        builder.Append(text, 0, start);
        AppendGrouped(builder, digits, width);
        builder.Append(text, suffixStart, text.Length - suffixStart);
        return builder.ToString();
    }

    /// <summary>Gets the index where the literal's numeric suffix begins.</summary>
    /// <param name="text">The literal token text.</param>
    /// <param name="start">The first digit index.</param>
    /// <returns>The suffix start index.</returns>
    private static int SuffixStart(string text, int start)
    {
        var i = text.Length;
        while (i > start && text[i - 1] is 'l' or 'L' or 'u' or 'U')
        {
            i--;
        }

        return i;
    }

    /// <summary>Copies a digit range with its separators removed.</summary>
    /// <param name="text">The literal token text.</param>
    /// <param name="start">The first digit index.</param>
    /// <param name="end">The index after the last digit.</param>
    /// <returns>The separator-free digits.</returns>
    private static string Compact(string text, int start, int end)
    {
        var builder = new StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            if (text[i] != '_')
            {
                builder.Append(text[i]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Appends digits grouped from the right at a fixed width.</summary>
    /// <param name="builder">The output builder.</param>
    /// <param name="digits">The separator-free digits.</param>
    /// <param name="width">The group width.</param>
    private static void AppendGrouped(StringBuilder builder, string digits, int width)
    {
        var lead = digits.Length % width;
        lead = lead == 0 ? width : lead;
        builder.Append(digits, 0, lead);
        for (var i = lead; i < digits.Length; i += width)
        {
            builder.Append('_').Append(digits, i, width);
        }
    }
}
