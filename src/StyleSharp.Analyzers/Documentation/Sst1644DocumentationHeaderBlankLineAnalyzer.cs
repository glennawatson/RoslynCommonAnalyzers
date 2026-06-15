// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Reports interior blank <c>///</c> lines in documentation comments (SST1644).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1644DocumentationHeaderBlankLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The number of slashes in a single-line documentation prefix.</summary>
    private const int DocumentationSlashCount = 3;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DocumentationRules.DocumentationHeaderNoBlankLines);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SingleLineDocumentationCommentTrivia);
    }

    /// <summary>Reports interior blank lines outside code-like XML elements.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var documentation = (DocumentationCommentTriviaSyntax)context.Node;
        var text = documentation.SyntaxTree.GetText(context.CancellationToken);
        var startLine = text.Lines.GetLineFromPosition(documentation.FullSpan.Start).LineNumber;
        var endLine = text.Lines.GetLineFromPosition(documentation.FullSpan.End - 1).LineNumber;
        for (var lineNumber = startLine + 1; lineNumber < endLine; lineNumber++)
        {
            var span = text.Lines[lineNumber].Span;
            if (!IsBlankDocumentationLine(text, span))
            {
                continue;
            }

            // A blank line inside a <code> or <example> sample (anywhere in the
            // comment, including nested in <remarks>/<para> or a CDATA block) is
            // intentional whitespace, so only blank prose lines are reported.
            if (IsInsideCodeLikeElement(documentation, span.Start))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DocumentationRules.DocumentationHeaderNoBlankLines,
                    Location.Create(documentation.SyntaxTree, span)));
        }
    }

    /// <summary>Returns whether a position falls inside a code-like element's whitespace-bearing span.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="position">The absolute source position to test.</param>
    /// <returns><see langword="true"/> when the position lies within a <c>&lt;code&gt;</c> or <c>&lt;example&gt;</c> element.</returns>
    private static bool IsInsideCodeLikeElement(DocumentationCommentTriviaSyntax documentation, int position)
    {
        var query = new CodeLikeSpanQuery(position);
        DescendantTraversalHelper.VisitDescendants<XmlElementSyntax, CodeLikeSpanQuery>(documentation, ref query, VisitCodeLikeElement);
        return query.Found;
    }

    /// <summary>Records whether the queried position falls inside a code-like element, stopping the walk once found.</summary>
    /// <param name="element">The visited XML element.</param>
    /// <param name="query">The position query state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> to stop.</returns>
    private static bool VisitCodeLikeElement(XmlElementSyntax element, ref CodeLikeSpanQuery query)
    {
        var name = element.StartTag.Name.LocalName.ValueText;
        if (name is not ("code" or "example"))
        {
            return true;
        }

        if (!element.FullSpan.Contains(query.Position))
        {
            return true;
        }

        query.Found = true;
        return false;
    }

    /// <summary>Returns whether a line contains whitespace and exactly three slashes.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="span">The line span.</param>
    /// <returns><see langword="true"/> for a blank documentation line.</returns>
    private static bool IsBlankDocumentationLine(SourceText text, TextSpan span)
    {
        var slashCount = 0;
        for (var i = span.Start; i < span.End; i++)
        {
            var character = text[i];
            if (character == '/')
            {
                slashCount++;
                continue;
            }

            if (!char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return slashCount == DocumentationSlashCount;
    }

    /// <summary>Mutable accumulator for the code-like-span containment query.</summary>
    /// <param name="Position">The absolute source position being tested.</param>
    private record struct CodeLikeSpanQuery(int Position)
    {
        /// <summary>Gets or sets a value indicating whether the position falls inside a code-like element.</summary>
        public bool Found { get; set; }
    }
}
