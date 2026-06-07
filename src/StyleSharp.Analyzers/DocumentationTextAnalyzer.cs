// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports documentation summaries whose prose falls short of basic quality checks: it
/// should begin with a capital letter (SST1628), contain more than one word (SST1630), be
/// made up mostly of letters rather than symbols (SST1631), and meet a minimum length
/// (SST1632). All four are opt-in, matching StyleCop's defaults.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DocumentationTextAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The minimum number of characters expected in a meaningful summary.</summary>
    private const int MinimumLength = 4;

    /// <summary>The documentation-comment node kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        DocumentationRules.TextBeginsWithCapital,
        DocumentationRules.TextContainsWhitespace,
        DocumentationRules.TextCharacterPercentage,
        DocumentationRules.TextMinimumLength,
        DocumentationRules.DocumentationTextNotEmpty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Runs the summary text-quality checks for a documentation comment.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var documentation = (DocumentationCommentTriviaSyntax)context.Node;
        CheckEmptySections(context, documentation);
        if (XmlDocumentationHelper.FindElement(documentation, "summary") is not { } summary)
        {
            return;
        }

        var text = XmlDocumentationHelper.NormalizedText(summary);
        if (text.Length == 0)
        {
            return;
        }

        var location = summary.GetLocation();
        CheckCapital(context, text, location);
        CheckWhitespace(context, text, location);
        CheckPercentage(context, text, location);
        CheckLength(context, text, location);
    }

    /// <summary>Reports empty non-summary section elements.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="documentation">The documentation comment.</param>
    private static void CheckEmptySections(SyntaxNodeAnalysisContext context, DocumentationCommentTriviaSyntax documentation)
    {
        for (var i = 0; i < documentation.Content.Count; i++)
        {
            if (documentation.Content[i] is not XmlElementSyntax element
                || !IsSectionElement(element.StartTag.Name.LocalName.ValueText.AsSpan())
                || HasChildElement(element)
                || XmlDocumentationHelper.NormalizedText(element).Length > 0)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.DocumentationTextNotEmpty, element.GetLocation()));
        }
    }

    /// <summary>Returns whether an XML element contains another XML element.</summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns><see langword="true"/> when a child element is present.</returns>
    private static bool HasChildElement(XmlElementSyntax element)
    {
        for (var i = 0; i < element.Content.Count; i++)
        {
            if (element.Content[i] is XmlElementSyntax or XmlEmptyElementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an element is a prose section that must contain text.</summary>
    /// <param name="name">The element name.</param>
    /// <returns><see langword="true"/> for handled section elements.</returns>
    private static bool IsSectionElement(ReadOnlySpan<char> name)
        => name.SequenceEqual("remarks".AsSpan())
            || name.SequenceEqual("para".AsSpan())
            || name.SequenceEqual("note".AsSpan())
            || name.SequenceEqual("example".AsSpan())
            || name.SequenceEqual("value".AsSpan())
            || name.SequenceEqual("returns".AsSpan());

    /// <summary>Reports a summary that does not begin with a capital letter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The normalized summary text.</param>
    /// <param name="location">The summary location.</param>
    private static void CheckCapital(SyntaxNodeAnalysisContext context, string text, Location location)
    {
        if (!char.IsLetter(text[0]) || !char.IsLower(text[0]))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TextBeginsWithCapital, location));
    }

    /// <summary>Reports a summary that is a single word (contains no whitespace).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The normalized summary text.</param>
    /// <param name="location">The summary location.</param>
    private static void CheckWhitespace(SyntaxNodeAnalysisContext context, string text, Location location)
    {
        if (text.IndexOf(' ') >= 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TextContainsWhitespace, location));
    }

    /// <summary>Reports a summary made up mostly of non-letter characters.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The normalized summary text.</param>
    /// <param name="location">The summary location.</param>
    private static void CheckPercentage(SyntaxNodeAnalysisContext context, string text, Location location)
    {
        var letters = 0;
        foreach (var character in text)
        {
            if (char.IsLetter(character) || char.IsWhiteSpace(character))
            {
                letters++;
            }
        }

        if (letters >= text.Length - letters)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TextCharacterPercentage, location));
    }

    /// <summary>Reports a summary shorter than the minimum meaningful length.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The normalized summary text.</param>
    /// <param name="location">The summary location.</param>
    private static void CheckLength(SyntaxNodeAnalysisContext context, string text, Location location)
    {
        if (text.Length >= MinimumLength)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TextMinimumLength, location));
    }
}
