// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CodeFixes;

namespace StyleSharp.Analyzers;

/// <summary>Fixes a Fix All scope one document at a time against the evolving solution, for fixes that add, remove or rename documents.</summary>
internal sealed class DocumentDiagnosticFixAllProvider : FixAllProvider
{
    /// <summary>The title shown for the Fix All code action.</summary>
    private readonly string _title;

    /// <summary>Applies the fix for every diagnostic reported in one document.</summary>
    private readonly Func<Solution, Document, ImmutableArray<Diagnostic>, CancellationToken, Task<Solution>> _fixDocumentAsync;

    /// <summary>Initializes a new instance of the <see cref="DocumentDiagnosticFixAllProvider"/> class.</summary>
    /// <param name="title">The title shown for the Fix All code action.</param>
    /// <param name="fixDocumentAsync">Applies the fix for every diagnostic reported in one document and returns the updated solution.</param>
    internal DocumentDiagnosticFixAllProvider(string title, Func<Solution, Document, ImmutableArray<Diagnostic>, CancellationToken, Task<Solution>> fixDocumentAsync)
    {
        _title = title;
        _fixDocumentAsync = fixDocumentAsync;
    }

    /// <inheritdoc/>
    public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        var documents = await CollectAsync(fixAllContext).ConfigureAwait(false);
        return documents.Count == 0
            ? null
            : CodeAction.Create(
                _title,
                cancellationToken => ApplyAsync(fixAllContext.Solution, documents, cancellationToken),
                equivalenceKey: fixAllContext.CodeActionEquivalenceKey ?? _title);
    }

    /// <summary>Collects the diagnostics in the Fix All scope, grouped by the document that reported them.</summary>
    /// <param name="fixAllContext">The Fix All context.</param>
    /// <returns>One entry per document that has at least one diagnostic.</returns>
    private static async Task<List<DocumentDiagnostics>> CollectAsync(FixAllContext fixAllContext)
    {
        var capacity = fixAllContext.Scope switch
        {
            FixAllScope.Document => 1,
            FixAllScope.Project => fixAllContext.Project.DocumentIds.Count,
            _ => 0
        };

        if (fixAllContext.Scope == FixAllScope.Solution)
        {
            foreach (var project in fixAllContext.Solution.Projects)
            {
                capacity += project.DocumentIds.Count;
            }
        }

        var result = new List<DocumentDiagnostics>(capacity);
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

            default:
                break;
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

        result.Add(new(document.Id, diagnostics));
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
                solution = await _fixDocumentAsync(solution, document, entry.Diagnostics, cancellationToken).ConfigureAwait(false);
            }
        }

        return solution;
    }

    /// <summary>One document and the diagnostics reported in it.</summary>
    /// <param name="DocumentId">The document's id, resolved against the evolving solution at apply time.</param>
    /// <param name="Diagnostics">The diagnostics reported in the document.</param>
    private readonly record struct DocumentDiagnostics(DocumentId DocumentId, ImmutableArray<Diagnostic> Diagnostics);
}
