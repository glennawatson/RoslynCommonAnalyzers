// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a word typed twice in a row in XML documentation text (SST1658), comparing the two
/// words case-insensitively. Only prose is scanned: the contents of <c>&lt;c&gt;</c>,
/// <c>&lt;code&gt;</c>, <c>&lt;see&gt;</c>, and <c>&lt;seealso&gt;</c> elements are skipped
/// entirely, and any punctuation between two words keeps them from forming a pair. A pair that
/// spans a documentation line break is still reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1658NoRepeatedWordsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The documentation-comment node kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DocumentationRules.NoRepeatedWords);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Scans each prose element of a documentation comment for adjacent repeated words.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var documentation = (DocumentationCommentTriviaSyntax)context.Node;
        foreach (var node in documentation.Content)
        {
            if (node is not XmlElementSyntax element
                || IsExcludedElement(element.StartTag.Name.LocalName.ValueText.AsSpan()))
            {
                continue;
            }

            // Each element gets its own word chain; a pair never spans two elements.
            var state = default(WordChainState);
            ScanContent(context, element.Content, ref state);
        }
    }

    /// <summary>Scans an element's content nodes, recursing into nested prose elements.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="content">The content nodes to scan.</param>
    /// <param name="state">The accumulating word-chain state.</param>
    private static void ScanContent(SyntaxNodeAnalysisContext context, SyntaxList<XmlNodeSyntax> content, ref WordChainState state)
    {
        foreach (var node in content)
        {
            switch (node)
            {
                case XmlTextSyntax text:
                {
                    ScanText(context, text, ref state);
                    break;
                }

                case XmlElementSyntax child:
                {
                    // An element tag is not whitespace, so it breaks the pair on both sides.
                    state.HasPrevious = false;
                    if (!IsExcludedElement(child.StartTag.Name.LocalName.ValueText.AsSpan()))
                    {
                        ScanContent(context, child.Content, ref state);
                    }

                    state.HasPrevious = false;
                    break;
                }

                default:
                {
                    state.HasPrevious = false;
                    break;
                }
            }
        }
    }

    /// <summary>Scans the tokens of one XML text node.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The XML text node.</param>
    /// <param name="state">The accumulating word-chain state.</param>
    private static void ScanText(SyntaxNodeAnalysisContext context, XmlTextSyntax text, ref WordChainState state)
    {
        foreach (var token in text.TextTokens)
        {
            if (token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken))
            {
                // A documentation line break is whitespace; the pending word survives it.
                continue;
            }

            if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
            {
                // An entity reference is neither a letter nor whitespace, so it breaks the chain.
                state.HasPrevious = false;
                continue;
            }

            ScanLiteral(context, token, ref state);
        }
    }

    /// <summary>Scans one literal token's value text for words, reporting adjacent repeats.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="token">The XML text literal token.</param>
    /// <param name="state">The accumulating word-chain state.</param>
    private static void ScanLiteral(SyntaxNodeAnalysisContext context, SyntaxToken token, ref WordChainState state)
    {
        var value = token.ValueText;
        var index = 0;
        while (index < value.Length)
        {
            var character = value[index];
            if (char.IsLetter(character))
            {
                var start = index;
                do
                {
                    index++;
                }
                while (index < value.Length && char.IsLetter(value[index]));

                CompleteWord(context, token, start, index - start, ref state);
                continue;
            }

            if (!char.IsWhiteSpace(character))
            {
                // Punctuation or a digit between two words breaks the pair.
                state.HasPrevious = false;
            }

            index++;
        }
    }

    /// <summary>Reports the completed word when it repeats the previous one, then makes it the new previous word.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="token">The token containing the completed word.</param>
    /// <param name="start">The word's start index within the token's value text.</param>
    /// <param name="length">The word's length.</param>
    /// <param name="state">The accumulating word-chain state.</param>
    private static void CompleteWord(SyntaxNodeAnalysisContext context, SyntaxToken token, int start, int length, ref WordChainState state)
    {
        if (state.HasPrevious
            && WordsMatch(state.PreviousToken.ValueText, state.PreviousStart, state.PreviousLength, token.ValueText, start, length))
        {
            var span = new TextSpan(token.SpanStart + start, length);
            context.ReportDiagnostic(Diagnostic.Create(
                DocumentationRules.NoRepeatedWords,
                Location.Create(context.Node.SyntaxTree, span),
                token.ValueText.Substring(start, length)));
        }

        state.PreviousToken = token;
        state.PreviousStart = start;
        state.PreviousLength = length;
        state.HasPrevious = true;
    }

    /// <summary>Compares two word slices case-insensitively (ordinal) without allocating.</summary>
    /// <param name="previousText">The text containing the previous word.</param>
    /// <param name="previousStart">The previous word's start index.</param>
    /// <param name="previousLength">The previous word's length.</param>
    /// <param name="currentText">The text containing the current word.</param>
    /// <param name="currentStart">The current word's start index.</param>
    /// <param name="currentLength">The current word's length.</param>
    /// <returns><see langword="true"/> when the words match.</returns>
    private static bool WordsMatch(string previousText, int previousStart, int previousLength, string currentText, int currentStart, int currentLength)
    {
        if (previousLength != currentLength)
        {
            return false;
        }

        for (var i = 0; i < previousLength; i++)
        {
            if (char.ToUpperInvariant(previousText[previousStart + i]) != char.ToUpperInvariant(currentText[currentStart + i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an element's content is exempt from the prose scan.</summary>
    /// <param name="name">The element name.</param>
    /// <returns><see langword="true"/> for code and reference elements.</returns>
    private static bool IsExcludedElement(ReadOnlySpan<char> name)
        => name.SequenceEqual("c".AsSpan())
            || name.SequenceEqual("code".AsSpan())
            || name.SequenceEqual("see".AsSpan())
            || name.SequenceEqual("seealso".AsSpan());

    /// <summary>Mutable accumulator tracking the most recent word while scanning element text.</summary>
    private record struct WordChainState
    {
        /// <summary>Gets or sets the token containing the previous word.</summary>
        public SyntaxToken PreviousToken { get; set; }

        /// <summary>Gets or sets the previous word's start index within its token's value text.</summary>
        public int PreviousStart { get; set; }

        /// <summary>Gets or sets the previous word's length.</summary>
        public int PreviousLength { get; set; }

        /// <summary>Gets or sets a value indicating whether a previous word is pending with only whitespace after it.</summary>
        public bool HasPrevious { get; set; }
    }
}
