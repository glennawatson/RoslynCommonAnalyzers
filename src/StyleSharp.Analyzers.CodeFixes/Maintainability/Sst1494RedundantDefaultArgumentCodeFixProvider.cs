// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Drops an argument that repeats its parameter's default (SST1494), along with every argument to its
/// right.
/// </summary>
/// <remarks>
/// <para>
/// The tail is removed as a unit on purpose. Deleting one argument from the middle of a positional list
/// would silently re-bind the arguments after it, so an argument is only safe to drop once everything
/// following it is gone. The analyzer only ever reports a trailing run — every argument after a reported one
/// is redundant too — so truncating the list at the reported argument removes nothing the rule did not
/// already ask to remove.
/// </para>
/// <para>
/// Before the edit is offered, the shortened call is bound speculatively and checked to still reach the same
/// method. The analyzer proved that once already; the fix proves it again against the tree it is about to
/// edit, because a Fix All pass can hand it a document that has moved on since the diagnostic was reported.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1494RedundantDefaultArgumentCodeFixProvider))]
[Shared]
public sealed class Sst1494RedundantDefaultArgumentCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.RedundantDefaultArgument.Id);

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
            if (!TryGetArgument(root, model, diagnostic, context.CancellationToken, out var argument))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Omit the argument that repeats the default",
                    _ => Task.FromResult(Apply(context.Document, root, argument!)),
                    equivalenceKey: nameof(Sst1494RedundantDefaultArgumentCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetArgument(editor.OriginalRoot, editor.SemanticModel, diagnostic, CancellationToken.None, out var argument))
        {
            return;
        }

        var list = (ArgumentListSyntax)argument!.Parent!;
        var index = list.Arguments.IndexOf(argument);
        editor.ReplaceNode(
            list,
            (current, _) => current is ArgumentListSyntax currentList
                ? Sst1494RedundantDefaultArgumentAnalyzer.TruncateAt(currentList, index)
                : current);
    }

    /// <summary>Removes the reported argument and every argument after it.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="argument">The reported argument.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ArgumentSyntax argument)
    {
        var list = (ArgumentListSyntax)argument.Parent!;
        var shortened = Sst1494RedundantDefaultArgumentAnalyzer.TruncateAt(list, list.Arguments.IndexOf(argument));
        return document.WithSyntaxRoot(root.ReplaceNode(list, shortened));
    }

    /// <summary>Resolves the diagnostic to an argument whose removal provably keeps the call where it was.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="argument">The reported argument when the omission is safe.</param>
    /// <returns><see langword="true"/> when the argument can be dropped.</returns>
    private static bool TryGetArgument(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        CancellationToken cancellationToken,
        out ArgumentSyntax? argument)
    {
        argument = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArgumentSyntax>();
        if (argument?.Parent is ArgumentListSyntax list
            && list.Parent is { } call
            && Sst1494RedundantDefaultArgumentAnalyzer.OmissionKeepsTheSameTarget(
                model,
                call,
                list.Arguments.IndexOf(argument),
                cancellationToken))
        {
            return true;
        }

        argument = null;
        return false;
    }
}
