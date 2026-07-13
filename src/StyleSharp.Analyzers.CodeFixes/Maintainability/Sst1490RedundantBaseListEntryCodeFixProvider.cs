// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a base-list entry the rest of the list already implies (SST1490). The entry that implied it
/// stays, so the list always keeps at least one entry and never has to be dropped whole.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1490RedundantBaseListEntryCodeFixProvider))]
[Shared]
public sealed class Sst1490RedundantBaseListEntryCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The smallest base list this fix will trim, leaving the implying entry behind.</summary>
    private const int MinimumTrimmableEntryCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.RedundantBaseListEntry.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryGetEntry(root, diagnostic) is not { } entry)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant base-list entry",
                    _ => Task.FromResult(Apply(context.Document, root, entry)),
                    equivalenceKey: nameof(Sst1490RedundantBaseListEntryCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetEntry(editor.OriginalRoot, diagnostic) is not { Parent: BaseListSyntax baseList } entry)
        {
            return;
        }

        // A list can hold two redundant entries, and the second edit runs against the list the first has
        // already trimmed — so the entry is found in the current node by what it names rather than by the
        // index it held. A base list cannot name the same interface twice, so the match is exact.
        editor.ReplaceNode(baseList, (current, _) => RemoveEntry((BaseListSyntax)current, entry));
    }

    /// <summary>Applies the fix for one redundant base-list entry.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="entry">The redundant entry.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BaseTypeSyntax entry)
    {
        var baseList = (BaseListSyntax)entry.Parent!;
        return document.WithSyntaxRoot(root.ReplaceNode(baseList, RemoveEntry(baseList, entry)));
    }

    /// <summary>Resolves the diagnostic's span to the base-list entry it reported.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported entry, or <see langword="null"/> when the shape no longer matches.</returns>
    private static BaseTypeSyntax? TryGetEntry(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BaseTypeSyntax>() is
            { Parent: BaseListSyntax { Types.Count: >= MinimumTrimmableEntryCount } } entry
            ? entry
            : null;

    /// <summary>Removes one entry from a base list, keeping the trivia that closed the list.</summary>
    /// <param name="baseList">The base list to trim.</param>
    /// <param name="entry">The entry to remove, as it was written in the original tree.</param>
    /// <returns>The trimmed base list.</returns>
    /// <remarks>
    /// The line break that follows the list lives as trailing trivia on its last entry, so removing that
    /// entry would take the line break with it and run the declaration into its own brace. Re-applying the
    /// original list's trailing trivia restores it, and is a no-op when the removed entry was not the last.
    /// </remarks>
    private static BaseListSyntax RemoveEntry(BaseListSyntax baseList, BaseTypeSyntax entry)
    {
        var entries = baseList.Types;
        if (entries.Count < MinimumTrimmableEntryCount)
        {
            return baseList;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            if (!SyntaxFactory.AreEquivalent(entries[i].Type, entry.Type))
            {
                continue;
            }

            return baseList
                .WithTypes(entries.RemoveAt(i))
                .WithTrailingTrivia(baseList.GetTrailingTrivia());
        }

        return baseList;
    }
}
