// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Resolves and rewrites the documentation element a documentation diagnostic was reported inside.</summary>
internal static class DocumentationElementFix
{
    /// <summary>Finds the innermost XML element around a diagnostic reported inside a documentation comment.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The element, or <see langword="null"/> when the diagnostic is not inside one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static XmlElementSyntax? FindElement(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true).FirstAncestorOrSelf<XmlElementSyntax>();

    /// <summary>Finds the documentation element of a member of one kind, and the type that declares the member.</summary>
    /// <typeparam name="TMember">The kind of member the documentation has to belong to.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="summary">The documentation element.</param>
    /// <param name="type">The type declaring the documented member.</param>
    /// <returns><see langword="true"/> when the element documents a <typeparamref name="TMember"/> inside a type.</returns>
    internal static bool TryFindMemberSummary<TMember>(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out XmlElementSyntax? summary,
        [NotNullWhen(true)] out TypeDeclarationSyntax? type)
        where TMember : SyntaxNode
    {
        summary = FindElement(root, diagnostic);
        type = summary is not null && XmlDocumentationHelper.DocumentedMember(summary) is TMember member
            ? member.FirstAncestorOrSelf<TypeDeclarationSyntax>()
            : null;
        return type is not null;
    }

    /// <summary>Appends the change that replaces a member's summary with the standard text its declaring type implies.</summary>
    /// <typeparam name="TMember">The kind of member the documentation has to belong to.</typeparam>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    /// <param name="standardSummary">Builds the standard summary text from the declaring type.</param>
    internal static void AppendStandardSummary<TMember>(SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes, Func<TypeDeclarationSyntax, string> standardSummary)
        where TMember : SyntaxNode
    {
        if (!TryFindMemberSummary<TMember>(root, diagnostic, out var summary, out var type))
        {
            return;
        }

        changes.Add(ReplaceSummary(summary, standardSummary(type)));
    }

    /// <summary>Builds the change that rewrites a summary element around new inner text.</summary>
    /// <param name="summary">The summary element.</param>
    /// <param name="innerText">The text the rewritten summary holds.</param>
    /// <returns>The change replacing the whole element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TextChange ReplaceSummary(XmlElementSyntax summary, string innerText) =>
        new(summary.Span, $"<summary>{innerText}</summary>");

    /// <summary>Writes one text change into a document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="change">The change to write.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ApplyAsync(Document document, TextChange change, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(change));
    }
}
