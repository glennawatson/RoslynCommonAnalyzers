// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports verbatim string literals that read better as C# 11 raw string literals (SST2243):
/// single-line verbatim literals carrying doubled-quote escapes whose value neither starts nor
/// ends with a quote, and any verbatim literal spanning multiple lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2243UseRawStringLiteralAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 11 language-version value (raw string literals).</summary>
    private const int CSharp11 = 1100;

    /// <summary>The shortest verbatim token text that can carry a doubled-quote escape or a line break.</summary>
    private const int MinimumTokenLength = 5;

    /// <summary>The index of the first content character in a verbatim token, after the <c>@"</c> prefix.</summary>
    private const int ContentStartIndex = 2;

    /// <summary>The character count a doubled-quote escape consumes.</summary>
    private const int QuoteEscapeLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseRawStringLiteral);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Reports verbatim, non-interpolated string literals that can become raw string literals.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var text = literal.Token.Text;
        if (text.Length < MinimumTokenLength || text[0] != '@')
        {
            return;
        }

        if (!IsLanguageVersionAtLeast(literal, CSharp11)
            || !IsConvertible(text)
            || literal.IsPartOfStructuredTrivia())
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseRawStringLiteral, literal.GetLocation()));
    }

    /// <summary>Returns whether a verbatim token's text is convertible to a raw string literal.</summary>
    /// <param name="text">The verbatim string literal token text, starting with <c>@</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the token spans multiple lines without whitespace-only value lines,
    /// or is single-line with at least one doubled-quote escape and a value that neither starts nor
    /// ends with a quote character.
    /// </returns>
    private static bool IsConvertible(string text)
    {
        var closingQuote = text.Length - 1;
        if (text[1] != '"' || text[closingQuote] != '"')
        {
            return false;
        }

        var hasQuoteEscape = false;
        var index = ContentStartIndex;
        while (index < closingQuote)
        {
            var c = text[index];
            if (c is '\n' or '\r')
            {
                return !HasWhitespaceOnlyLine(text, closingQuote);
            }

            if (c == '"')
            {
                hasQuoteEscape = true;
                index += QuoteEscapeLength;
                continue;
            }

            index++;
        }

        return hasQuoteEscape && text[ContentStartIndex] != '"' && text[closingQuote - 1] != '"';
    }

    /// <summary>Returns whether the multi-line value contains a line made only of whitespace.</summary>
    /// <param name="text">The verbatim string literal token text, starting with <c>@</c>.</param>
    /// <param name="closingQuote">The index of the closing quote character.</param>
    /// <returns>
    /// <see langword="true"/> when any value line is non-empty yet holds only spaces or tabs; the raw
    /// string conversion would normalize such a line to empty and change the value, so it is skipped.
    /// </returns>
    private static bool HasWhitespaceOnlyLine(string text, int closingQuote)
    {
        var lineLength = 0;
        var lineHasContent = false;
        for (var index = ContentStartIndex; index < closingQuote; index++)
        {
            var c = text[index];
            if (c is '\n' or '\r')
            {
                if (lineLength > 0 && !lineHasContent)
                {
                    return true;
                }

                lineLength = 0;
                lineHasContent = false;
                continue;
            }

            lineLength++;
            if (c is not (' ' or '\t'))
            {
                lineHasContent = true;
            }
        }

        return lineLength > 0 && !lineHasContent;
    }

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;
}
