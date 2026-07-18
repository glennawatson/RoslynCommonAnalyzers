// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Folds a run of member copies into one deconstruction declaration (SST2247):
/// <c>var a = pair.Item1; var b = pair.Item2;</c> becomes <c>var (a, b) = pair;</c>. The first
/// statement is replaced by the deconstruction and the remaining copies are removed; the source
/// value stays untouched.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2247MemberCopyDeconstructionCodeFixProvider))]
[Shared]
public sealed class Sst2247MemberCopyDeconstructionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.DeconstructMemberCopies.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryResolve(root, model, diagnostic, context.CancellationToken, out var candidate))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Fold the member copies into a deconstruction",
                    _ => Task.FromResult(Apply(context.Document, root, candidate)),
                    equivalenceKey: nameof(Sst2247MemberCopyDeconstructionCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryResolve(editor.OriginalRoot, editor.SemanticModel, diagnostic, CancellationToken.None, out var candidate))
        {
            return;
        }

        editor.ReplaceNode(candidate.FirstStatement, BuildDeconstruction(candidate));
        for (var i = 1; i < candidate.Count; i++)
        {
            editor.RemoveNode(candidate.Block.Statements[candidate.StartIndex + i], SyntaxRemoveOptions.KeepNoTrivia);
        }
    }

    /// <summary>Resolves the reported run from a diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">A token that cancels resolution.</param>
    /// <param name="candidate">The resolved run.</param>
    /// <returns><see langword="true"/> when the run still matches.</returns>
    private static bool TryResolve(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        CancellationToken cancellationToken,
        out Sst2247MemberCopyDeconstructionAnalyzer.MemberCopyDeconstruction candidate)
    {
        candidate = default;
        return root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is { } first
            && Sst2247MemberCopyDeconstructionAnalyzer.TryGetCandidate(first, model, cancellationToken, out candidate);
    }

    /// <summary>Rewrites the block, folding the run into one deconstruction declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="candidate">The resolved run.</param>
    /// <returns>The updated document.</returns>
    private static Document Apply(Document document, SyntaxNode root, Sst2247MemberCopyDeconstructionAnalyzer.MemberCopyDeconstruction candidate)
    {
        var block = candidate.Block;
        var statements = new List<StatementSyntax>(block.Statements.Count - candidate.Count + 1);
        for (var i = 0; i < block.Statements.Count; i++)
        {
            if (i == candidate.StartIndex)
            {
                statements.Add(BuildDeconstruction(candidate));
            }
            else if (i < candidate.StartIndex || i >= candidate.StartIndex + candidate.Count)
            {
                statements.Add(block.Statements[i]);
            }
        }

        var updated = root.ReplaceNode(block, block.WithStatements(SyntaxFactory.List(statements)));
        return document.WithSyntaxRoot(updated);
    }

    /// <summary>Builds the <c>var (a, b, …) = source;</c> statement for a run.</summary>
    /// <param name="candidate">The resolved run.</param>
    /// <returns>The deconstruction statement carrying the run's outer trivia.</returns>
    private static StatementSyntax BuildDeconstruction(Sst2247MemberCopyDeconstructionAnalyzer.MemberCopyDeconstruction candidate)
        => SyntaxFactory.ParseStatement($"var ({string.Join(", ", candidate.Names)}) = {candidate.SourceName};")
            .WithLeadingTrivia(candidate.FirstStatement.GetLeadingTrivia())
            .WithTrailingTrivia(candidate.LastStatement.GetTrailingTrivia());
}
