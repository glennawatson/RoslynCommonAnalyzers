// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CodeFixes;

namespace StyleSharp.Analyzers;

/// <summary>
/// A <see cref="FixAllProvider"/> for fixes whose change is at the solution level — adding or
/// renaming documents — which <see cref="WellKnownFixAllProviders.BatchFixer"/> (text-edit merging
/// only) cannot carry. It groups the diagnostics in the requested scope by document and applies the
/// per-document fix to the evolving solution one document at a time, so each fix sees the result of
/// the previous one (and a document already removed by an earlier linked-file fix is skipped).
/// </summary>
internal abstract class DocumentDiagnosticFixAllProvider : FixAllProvider
{
    /// <summary>Gets the title shown for the Fix All code action.</summary>
    protected abstract string Title { get; }

    /// <inheritdoc/>
    public sealed override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        var documents = await CollectAsync(fixAllContext).ConfigureAwait(false);
        return documents.Count == 0
            ? null
            : CodeAction.Create(
                Title,
                cancellationToken => ApplyAsync(fixAllContext.Solution, documents, cancellationToken),
                equivalenceKey: fixAllContext.CodeActionEquivalenceKey ?? Title);
    }

    /// <summary>Applies the fix for every diagnostic reported in one document to the evolving solution.</summary>
    /// <param name="solution">The current (evolving) solution.</param>
    /// <param name="document">The document to fix.</param>
    /// <param name="diagnostics">The diagnostics reported in that document.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated solution.</returns>
    protected abstract Task<Solution> FixDocumentAsync(Solution solution, Document document, ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken);

    /// <summary>Collects the diagnostics in the Fix All scope, grouped by the document that reported them.</summary>
    /// <param name="fixAllContext">The Fix All context.</param>
    /// <returns>One entry per document that has at least one diagnostic.</returns>
    private static async Task<List<DocumentDiagnostics>> CollectAsync(FixAllContext fixAllContext)
    {
        var result = new List<DocumentDiagnostics>();
        switch (fixAllContext.Scope)
        {
            case FixAllScope.Document when fixAllContext.Document is { } document:
            {
                await AddDocumentAsync(fixAllContext, document, result).ConfigureAwait(false);
                break;
            }

            case FixAllScope.Project:
            {
                await AddProjectAsync(fixAllContext, fixAllContext.Project, result).ConfigureAwait(false);
                break;
            }

            case FixAllScope.Solution:
            {
                foreach (var project in fixAllContext.Solution.Projects)
                {
                    await AddProjectAsync(fixAllContext, project, result).ConfigureAwait(false);
                }

                break;
            }
        }

        return result;
    }

    /// <summary>Adds every document of a project that reported a diagnostic.</summary>
    /// <param name="fixAllContext">The Fix All context.</param>
    /// <param name="project">The project to scan.</param>
    /// <param name="result">The accumulating list.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task AddProjectAsync(FixAllContext fixAllContext, Project project, List<DocumentDiagnostics> result)
    {
        foreach (var document in project.Documents)
        {
            await AddDocumentAsync(fixAllContext, document, result).ConfigureAwait(false);
        }
    }

    /// <summary>Adds a document and its diagnostics when it reported at least one.</summary>
    /// <param name="fixAllContext">The Fix All context.</param>
    /// <param name="document">The document to inspect.</param>
    /// <param name="result">The accumulating list.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task AddDocumentAsync(FixAllContext fixAllContext, Document document, List<DocumentDiagnostics> result)
    {
        var diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);
        if (diagnostics.IsEmpty)
        {
            return;
        }

        result.Add(new DocumentDiagnostics(document.Id, diagnostics));
    }

    /// <summary>Applies every collected document's fix in turn, threading the evolving solution through.</summary>
    /// <param name="solution">The starting solution.</param>
    /// <param name="documents">The per-document diagnostics to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The fully updated solution.</returns>
    private async Task<Solution> ApplyAsync(Solution solution, List<DocumentDiagnostics> documents, CancellationToken cancellationToken)
    {
        for (var index = 0; index < documents.Count; index++)
        {
            var entry = documents[index];
            var document = solution.GetDocument(entry.DocumentId);

            // A null document means an earlier fix on a linked copy of the same physical file handled it.
            if (document is not null)
            {
                solution = await FixDocumentAsync(solution, document, entry.Diagnostics, cancellationToken).ConfigureAwait(false);
            }
        }

        return solution;
    }

    /// <summary>One document and the diagnostics reported in it.</summary>
    /// <param name="DocumentId">The document's id (resolved against the evolving solution at apply time).</param>
    /// <param name="Diagnostics">The diagnostics reported in the document.</param>
    private readonly record struct DocumentDiagnostics(DocumentId DocumentId, ImmutableArray<Diagnostic> Diagnostics);
}
