// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>
/// A <see cref="DocumentBasedFixAllProvider"/> that applies every diagnostic in a document in a single
/// pass: it creates one <see cref="DocumentEditor"/> and lets the owning fix register each diagnostic's
/// edits against the original root, then materialises the changed document once. This avoids
/// <see cref="WellKnownFixAllProviders.BatchFixer"/>'s clone-and-reparse of the document per diagnostic.
/// </summary>
internal sealed class BatchEditFixAllProvider : DocumentBasedFixAllProvider
{
    /// <summary>The shared provider instance.</summary>
    public static readonly BatchEditFixAllProvider Instance = new();

    /// <summary>Returns diagnostics with duplicate document edits removed.</summary>
    /// <param name="diagnostics">The diagnostics to filter.</param>
    /// <returns>The diagnostics that should register batch edits.</returns>
    internal static IEnumerable<Diagnostic> UniqueDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        var seen = new HashSet<DiagnosticEditKey>();
        foreach (var diagnostic in diagnostics)
        {
            var key = new DiagnosticEditKey(diagnostic.Id, diagnostic.Location.SourceSpan);
            if (seen.Add(key))
            {
                yield return diagnostic;
            }
        }
    }

    /// <summary>Returns diagnostics with duplicate resolved edit targets removed.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="fix">The batch fix.</param>
    /// <param name="diagnostics">The diagnostics to filter.</param>
    /// <returns>The diagnostics that should register batch edits.</returns>
    internal static IEnumerable<Diagnostic> UniqueDiagnostics(SyntaxNode root, IBatchFixableCodeFix fix, ImmutableArray<Diagnostic> diagnostics)
    {
        var seen = new HashSet<DiagnosticEditKey>();
        var keyProvider = fix as IBatchEditKeyProvider;
        foreach (var diagnostic in diagnostics)
        {
            var span = keyProvider is not null && keyProvider.TryGetBatchEditSpan(root, diagnostic, out var editSpan)
                ? editSpan
                : diagnostic.Location.SourceSpan;
            var key = new DiagnosticEditKey(diagnostic.Id, span);
            if (seen.Add(key))
            {
                yield return diagnostic;
            }
        }
    }

    /// <summary>Registers one batch edit, ignoring Roslyn's stale-node error for duplicate edit targets.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="fix">The batch fix.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdit(DocumentEditor editor, IBatchFixableCodeFix fix, Diagnostic diagnostic)
    {
        try
        {
            fix.RegisterBatchEdits(editor, diagnostic);
        }
        catch (InvalidOperationException exception) when (IsDuplicateEditTarget(exception))
        {
            // The first edit for this syntax node already won; later linked-document duplicates can be skipped.
        }
    }

    /// <inheritdoc/>
    protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty || fixAllContext.CodeFixProvider is not IBatchFixableCodeFix fix)
        {
            return document;
        }

        var editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
        var orderedDiagnostics = GetOrderedDiagnostics(editor.OriginalRoot, fix, diagnostics);
        for (var i = 0; i < orderedDiagnostics.Count; i++)
        {
            RegisterBatchEdit(editor, fix, orderedDiagnostics[i]);
        }

        return editor.GetChangedDocument();
    }

    /// <summary>Returns unique diagnostics in an order that lets nested edits compose before parent replacements.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="fix">The batch fix.</param>
    /// <param name="diagnostics">The diagnostics to order.</param>
    /// <returns>The ordered diagnostics.</returns>
    private static List<Diagnostic> GetOrderedDiagnostics(SyntaxNode root, IBatchFixableCodeFix fix, ImmutableArray<Diagnostic> diagnostics)
    {
        var ordered = new List<Diagnostic>(UniqueDiagnostics(root, fix, diagnostics));
        ordered.Sort(CompareDiagnosticsForBatchEdit);
        return ordered;
    }

    /// <summary>Orders diagnostics from innermost/later spans to outer spans for SyntaxEditor composition.</summary>
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

    /// <summary>Returns whether an exception represents a duplicate syntax edit target already consumed by <see cref="SyntaxEditor"/>.</summary>
    /// <param name="exception">The exception thrown while registering a batch edit.</param>
    /// <returns><see langword="true"/> when the edit can be skipped because the target was already replaced or removed.</returns>
    private static bool IsDuplicateEditTarget(InvalidOperationException exception)
        => exception.Message.StartsWith("GetCurrentNode returned null", StringComparison.Ordinal);

    /// <summary>A unique document edit target for diagnostics already grouped by document.</summary>
    /// <param name="Id">The diagnostic id.</param>
    /// <param name="Span">The diagnostic source span.</param>
    private readonly record struct DiagnosticEditKey(string Id, TextSpan Span);
}
