// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds an <c>&lt;exception cref="..."&gt;</c> skeleton to a member's documentation for each directly-thrown
/// exception type it does not yet describe (SST1662). The new elements are appended after the existing
/// documentation lines, ready for a description to be filled in.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1662ThrownExceptionDocumentationCodeFixProvider))]
[Shared]
public sealed class Sst1662ThrownExceptionDocumentationCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(DocumentationRules.ThrownExceptionDocumentation.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryBuildChange(text, root, diagnostic, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add <exception> documentation for the thrown types",
                    cancellationToken => AddAsync(context.Document, diagnostic, cancellationToken),
                    equivalenceKey: nameof(Sst1662ThrownExceptionDocumentationCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryBuildChange(text, root, diagnostic, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Applies the <c>&lt;exception&gt;</c> insertion to the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> AddAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return root is null || !TryBuildChange(text, root, diagnostic, out var change) ? document : document.WithText(text.WithChanges(change));
    }

    /// <summary>Builds the text change that appends the missing <c>&lt;exception&gt;</c> elements.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="change">The insertion change when one is produced.</param>
    /// <returns><see langword="true"/> when a change was built.</returns>
    private static bool TryBuildChange(SourceText text, SyntaxNode root, Diagnostic diagnostic, out TextChange change)
    {
        change = default;

        if (!diagnostic.Properties.TryGetValue(Sst1662ThrownExceptionDocumentationAnalyzer.ThrownTypesKey, out var joined)
            || string.IsNullOrEmpty(joined)
            || !diagnostic.Properties.TryGetValue(Sst1662ThrownExceptionDocumentationAnalyzer.ThrownDescriptionsKey, out var joinedDescriptions))
        {
            return false;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() is not { } member
            || XmlDocumentationHelper.GetDocumentationComment(member) is not { } documentation)
        {
            return false;
        }

        var trivia = documentation.ParentTrivia;
        if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
        {
            // Only the '///' comment form is rewritten; a '/** */' comment is left for a manual edit.
            return false;
        }

        var full = trivia.FullSpan;
        var firstLine = text.Lines.GetLineFromPosition(full.Start);
        var indent = text.ToString(TextSpan.FromBounds(firstLine.Start, full.Start));
        var newLine = text.ToString(TextSpan.FromBounds(firstLine.End, firstLine.EndIncludingLineBreak));
        if (newLine.Length == 0)
        {
            newLine = "\n";
        }

        var elements = BuildElements(joined!, joinedDescriptions, indent, newLine);
        if (elements.Length == 0)
        {
            return false;
        }

        change = new(new TextSpan(full.End, 0), elements);
        return true;
    }

    /// <summary>Builds the <c>&lt;exception&gt;</c> lines for the types whose trigger is known.</summary>
    /// <param name="joined">The newline-separated cref forms.</param>
    /// <param name="joinedDescriptions">The newline-separated descriptions, aligned with the cref forms.</param>
    /// <param name="indent">The indentation the documentation sits at.</param>
    /// <param name="newLine">The line ending the document uses.</param>
    /// <returns>The lines to insert, empty when no type has a description.</returns>
    /// <remarks>
    /// A type with no description is skipped rather than written empty. An undescribed element is what
    /// SST1665 reports, and it treats one as worse than the missing element this rule found, so a throw
    /// that nothing guards is left for its author to explain.
    /// </remarks>
    private static string BuildElements(string joined, string? joinedDescriptions, string indent, string newLine)
    {
        var crefCount = 1;
        foreach (var character in joined)
        {
            if (character == '\n')
            {
                crefCount++;
            }
        }

        var descriptions = joinedDescriptions ?? string.Empty;
        var elementLength = indent.Length + "/// <exception cref=\"".Length + "\">".Length + "</exception>".Length + newLine.Length;
        var capacity = joined.Length + descriptions.Length + (crefCount * elementLength);
        var builder = new StringBuilder(capacity);
        var crefStart = 0;
        var descriptionStart = 0;
        while (crefStart < joined.Length)
        {
            var crefEnd = joined.IndexOf('\n', crefStart);
            if (crefEnd < 0)
            {
                crefEnd = joined.Length;
            }

            var descriptionEnd = descriptions.IndexOf('\n', descriptionStart);
            if (descriptionEnd < 0)
            {
                descriptionEnd = descriptions.Length;
            }

            if (crefEnd > crefStart && descriptionEnd > descriptionStart)
            {
                _ = builder.Append(indent).Append("/// <exception cref=\"").Append(joined, crefStart, crefEnd - crefStart).Append("\">")
                    .Append(descriptions, descriptionStart, descriptionEnd - descriptionStart).Append("</exception>").Append(newLine);
            }

            crefStart = crefEnd + 1;
            descriptionStart = descriptionEnd < descriptions.Length ? descriptionEnd + 1 : descriptions.Length;
        }

        return builder.ToString();
    }
}
