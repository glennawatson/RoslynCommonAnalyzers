// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Reports interior blank <c>///</c> lines in documentation comments (SST1644).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DocumentationHeaderBlankLineAnalyzer : DiagnosticAnalyzer
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
        if (ContainsCodeLikeElement(documentation))
        {
            return;
        }

        var text = documentation.SyntaxTree.GetText(context.CancellationToken);
        var startLine = text.Lines.GetLineFromPosition(documentation.FullSpan.Start).LineNumber;
        var endLine = text.Lines.GetLineFromPosition(documentation.FullSpan.End - 1).LineNumber;
        for (var lineNumber = startLine + 1; lineNumber < endLine; lineNumber++)
        {
            var span = text.Lines[lineNumber].Span;
            if (IsBlankDocumentationLine(text, span))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DocumentationRules.DocumentationHeaderNoBlankLines,
                        Location.Create(documentation.SyntaxTree, span)));
            }
        }
    }

    /// <summary>Returns whether intentional whitespace-bearing elements are present.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <returns><see langword="true"/> when a code-like element exists.</returns>
    private static bool ContainsCodeLikeElement(DocumentationCommentTriviaSyntax documentation)
        => XmlDocumentationHelper.FindElement(documentation, "code") is not null
            || XmlDocumentationHelper.FindElement(documentation, "example") is not null;

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
}
