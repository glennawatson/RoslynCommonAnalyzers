// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>Applies every diagnostic in a document through one <see cref="DocumentEditor"/> and materialises the result once.</summary>
internal sealed class BatchEditFixAllProvider : DocumentBasedFixAllProvider
{
    /// <summary>Registers one diagnostic's edits against the editor's original root.</summary>
    private readonly Action<DocumentEditor, Diagnostic> _registerEdits;

    /// <summary>Resolves the span a diagnostic's edit replaces, or <see langword="null"/> to key edits by the diagnostic's own span.</summary>
    private readonly TryFunc<SyntaxNode, Diagnostic, TextSpan>? _tryGetEditSpan;

    /// <summary>Initializes a new instance of the <see cref="BatchEditFixAllProvider"/> class.</summary>
    /// <param name="registerEdits">Registers one diagnostic's edits against the editor's original root.</param>
    internal BatchEditFixAllProvider(Action<DocumentEditor, Diagnostic> registerEdits) => _registerEdits = registerEdits;

    /// <summary>Initializes a new instance of the <see cref="BatchEditFixAllProvider"/> class that deduplicates by resolved edit span.</summary>
    /// <param name="registerEdits">Registers one diagnostic's edits against the editor's original root.</param>
    /// <param name="tryGetEditSpan">Resolves the span a diagnostic's edit replaces.</param>
    internal BatchEditFixAllProvider(Action<DocumentEditor, Diagnostic> registerEdits, TryFunc<SyntaxNode, Diagnostic, TextSpan> tryGetEditSpan)
        : this(registerEdits) => _tryGetEditSpan = tryGetEditSpan;

    /// <summary>Initializes a new instance of the <see cref="BatchEditFixAllProvider"/> class for a fix that replaces one node per diagnostic.</summary>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    internal BatchEditFixAllProvider(Func<SyntaxNode, Diagnostic, NodeReplacement?> tryRewrite)
        : this((editor, diagnostic) => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, tryRewrite))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BatchEditFixAllProvider"/> class for a fix that replaces one node per diagnostic using the semantic model.</summary>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    internal BatchEditFixAllProvider(Func<SyntaxNode, SemanticModel, Diagnostic, NodeReplacement?> tryRewrite)
        : this((editor, diagnostic) => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, tryRewrite))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchEditFixAllProvider"/> class for a fix that replaces one node per diagnostic
    /// using the semantic model and deduplicates by resolved edit span.
    /// </summary>
    /// <param name="tryRewrite">The provider's edit derivation.</param>
    /// <param name="tryGetEditSpan">Resolves the span a diagnostic's edit replaces.</param>
    internal BatchEditFixAllProvider(Func<SyntaxNode, SemanticModel, Diagnostic, NodeReplacement?> tryRewrite, TryFunc<SyntaxNode, Diagnostic, TextSpan> tryGetEditSpan)
        : this((editor, diagnostic) => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, tryRewrite), tryGetEditSpan)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchEditFixAllProvider"/> class for a fix that rewrites one resolved node per
    /// diagnostic from the form it has after earlier edits in the batch, without building a replacement from the original.
    /// </summary>
    /// <param name="resolveTarget">Resolves the node a diagnostic's edit replaces, or <see langword="null"/> when the shape no longer matches.</param>
    /// <param name="rewriteCurrent">Rewrites the node as it stands when the editor applies the edit.</param>
    internal BatchEditFixAllProvider(Func<SyntaxNode, Diagnostic, SyntaxNode?> resolveTarget, Func<SyntaxNode, SyntaxGenerator, SyntaxNode> rewriteCurrent)
        : this((editor, diagnostic) =>
        {
            if (resolveTarget(editor.OriginalRoot, diagnostic) is { } target)
            {
                editor.ReplaceNode(target, rewriteCurrent);
            }
        })
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BatchEditFixAllProvider"/> class for a fix that removes one node per diagnostic.</summary>
    /// <param name="trySelect">Resolves the node to remove.</param>
    internal BatchEditFixAllProvider(Func<SyntaxNode, Diagnostic, NodeRemoval?> trySelect)
        : this((editor, diagnostic) => RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, trySelect))
    {
    }

    /// <summary>Returns diagnostics with duplicate id and span pairs removed.</summary>
    /// <param name="diagnostics">The diagnostics to filter.</param>
    /// <returns>The diagnostics that should register batch edits.</returns>
    internal static IEnumerable<Diagnostic> UniqueDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        var seen = new Dictionary<DiagnosticEditKey, bool>(diagnostics.Length);
        foreach (var diagnostic in diagnostics)
        {
            var key = new DiagnosticEditKey(diagnostic.Id, diagnostic.Location.SourceSpan);
            if (seen.ContainsKey(key))
            {
                continue;
            }

            seen.Add(key, true);
            yield return diagnostic;
        }
    }

    /// <summary>Collects the diagnostics with distinct edit targets, in their original order.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostics">The diagnostics to filter.</param>
    /// <returns>The diagnostics that should register batch edits.</returns>
    internal List<Diagnostic> CollectUniqueDiagnostics(SyntaxNode root, ImmutableArray<Diagnostic> diagnostics)
    {
        var result = new List<Diagnostic>(diagnostics.Length);

        // netstandard2.0 has no HashSet capacity constructor; the dictionary value is never read.
        var seen = new Dictionary<DiagnosticEditKey, bool>(diagnostics.Length);
        for (var i = 0; i < diagnostics.Length; i++)
        {
            var diagnostic = diagnostics[i];
            var key = EditKey(root, diagnostic);
            if (seen.ContainsKey(key))
            {
                continue;
            }

            seen.Add(key, true);
            result.Add(diagnostic);
        }

        return result;
    }

    /// <summary>Registers one diagnostic's edits, skipping a target an earlier edit already consumed.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal void RegisterBatchEdit(DocumentEditor editor, Diagnostic diagnostic)
    {
        try
        {
            _registerEdits(editor, diagnostic);
        }
        catch (InvalidOperationException exception) when (IsDuplicateEditTarget(exception))
        {
            // The first edit for this syntax node already won; later linked-document duplicates can be skipped.
        }
    }

    /// <inheritdoc/>
    protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty)
        {
            return document;
        }

        var editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
        var orderedDiagnostics = GetOrderedDiagnostics(editor.OriginalRoot, diagnostics);
        for (var i = 0; i < orderedDiagnostics.Count; i++)
        {
            RegisterBatchEdit(editor, orderedDiagnostics[i]);
        }

        try
        {
            return editor.GetChangedDocument();
        }
        catch (InvalidOperationException exception) when (IsDuplicateEditTarget(exception))
        {
            // One edit's target was consumed by another's replacement. That only surfaces once the whole
            // batch is materialised, so the conflicting edits have to be found by applying them in turn.
            return await ApplyCompatibleEditsAsync(document, orderedDiagnostics, fixAllContext.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Orders diagnostics from later and inner spans to outer spans so nested edits compose before parent replacements.</summary>
    /// <param name="left">The first diagnostic.</param>
    /// <param name="right">The second diagnostic.</param>
    /// <returns>The comparison result.</returns>
    private static int CompareDiagnosticsForBatchEdit(Diagnostic left, Diagnostic right)
    {
        var leftSpan = left.Location.SourceSpan;
        var rightSpan = right.Location.SourceSpan;
        var startComparison = rightSpan.Start.CompareTo(leftSpan.Start);
        return startComparison != 0
            ? startComparison
            : leftSpan.Length.CompareTo(rightSpan.Length);
    }

    /// <summary>Returns whether an exception reports a syntax edit target that <see cref="SyntaxEditor"/> already replaced or removed.</summary>
    /// <param name="exception">The exception thrown while registering or applying a batch edit.</param>
    /// <returns><see langword="true"/> when the edit can be skipped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDuplicateEditTarget(InvalidOperationException exception) =>
        exception.Message.StartsWith("GetCurrentNode returned null", StringComparison.Ordinal);

    /// <summary>Applies the edits that compose, dropping any whose target another edit has consumed.</summary>
    /// <param name="document">The unedited document.</param>
    /// <param name="diagnostics">The diagnostics in batch-edit order.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The document carrying every edit that could be applied.</returns>
    private async Task<Document> ApplyCompatibleEditsAsync(Document document, List<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var accepted = new List<Diagnostic>(diagnostics.Count);
        var result = document;
        for (var i = 0; i < diagnostics.Count; i++)
        {
            accepted.Add(diagnostics[i]);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            for (var j = 0; j < accepted.Count; j++)
            {
                RegisterBatchEdit(editor, accepted[j]);
            }

            try
            {
                result = editor.GetChangedDocument();
            }
            catch (InvalidOperationException exception) when (IsDuplicateEditTarget(exception))
            {
                accepted.RemoveAt(accepted.Count - 1);
            }
        }

        return result;
    }

    /// <summary>Returns the diagnostics with distinct edit targets, in batch-edit order.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostics">The diagnostics to order.</param>
    /// <returns>The ordered diagnostics.</returns>
    private List<Diagnostic> GetOrderedDiagnostics(SyntaxNode root, ImmutableArray<Diagnostic> diagnostics)
    {
        var ordered = new List<Diagnostic>(diagnostics.Length);
        for (var i = 0; i < diagnostics.Length; i++)
        {
            ordered.Add(diagnostics[i]);
        }

        ordered.Sort(CompareDiagnosticsForBatchEdit);

        if (_tryGetEditSpan is not null)
        {
            return DeduplicateByEditSpan(root, ordered);
        }

        // Keyed by the diagnostic's own span, duplicates are neighbours after the sort, so no set is needed.
        var write = 0;
        for (var read = 0; read < ordered.Count; read++)
        {
            if (read > 0
                && ordered[read].Location.SourceSpan == ordered[write - 1].Location.SourceSpan
                && string.Equals(ordered[read].Id, ordered[write - 1].Id, StringComparison.Ordinal))
            {
                continue;
            }

            ordered[write] = ordered[read];
            write++;
        }

        ordered.RemoveRange(write, ordered.Count - write);
        return ordered;
    }

    /// <summary>Removes diagnostics whose resolved edit targets collide, preserving the batch order.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="ordered">The diagnostics in batch-edit order.</param>
    /// <returns>The diagnostics with duplicate edit targets removed.</returns>
    private List<Diagnostic> DeduplicateByEditSpan(SyntaxNode root, List<Diagnostic> ordered)
    {
        // A resolved span need not match the diagnostic's span, so equal keys are not necessarily adjacent.
        var seen = new Dictionary<DiagnosticEditKey, bool>(ordered.Count);
        var write = 0;
        for (var read = 0; read < ordered.Count; read++)
        {
            var diagnostic = ordered[read];
            var key = EditKey(root, diagnostic);
            if (seen.ContainsKey(key))
            {
                continue;
            }

            seen.Add(key, true);
            ordered[write] = diagnostic;
            write++;
        }

        ordered.RemoveRange(write, ordered.Count - write);
        return ordered;
    }

    /// <summary>Returns the key identifying the syntax a diagnostic's edit targets.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The diagnostic id paired with the resolved edit span, or with the diagnostic's own span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DiagnosticEditKey EditKey(SyntaxNode root, Diagnostic diagnostic) =>
        new(diagnostic.Id, _tryGetEditSpan is not null && _tryGetEditSpan(root, diagnostic, out var span) ? span : diagnostic.Location.SourceSpan);

    /// <summary>A document edit target for diagnostics already grouped by document.</summary>
    /// <param name="Id">The diagnostic id.</param>
    /// <param name="Span">The edited span.</param>
    private readonly record struct DiagnosticEditKey(string Id, TextSpan Span);
}
