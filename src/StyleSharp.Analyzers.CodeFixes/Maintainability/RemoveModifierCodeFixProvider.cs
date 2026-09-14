// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes the modifier reported by SST1419 (redundant) or SST1427 (<c>protected</c> in a sealed type).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveModifierCodeFixProvider))]
[Shared]
public sealed class RemoveModifierCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.NoRedundantModifier.Id,
        MaintainabilityRules.NoProtectedInSealed.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            if (!ReportedModifier.TryFind(root, diagnostic, out var declaration, out var token, out _))
            {
                // A redundant 'checked'/'unchecked' context (SST1419) is not a member modifier; removing it
                // is a structural rewrite, not a token deletion, so no fix is offered for that shape.
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove redundant modifier",
                    cancellationToken => Task.FromResult(RemoveModifier(context.Document, root, declaration, token)),
                    equivalenceKey: nameof(RemoveModifierCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        // Remove by index, computed lazily against the current (tracked) node so a parent edit applied
        // first keeps the descendant's annotations — see BatchEditFixAllProvider. The token's absolute
        // span shifts under earlier edits, so we cannot match it directly at apply time.
        if (!ReportedModifier.TryFind(editor.OriginalRoot, diagnostic, out var declaration, out _, out var modifierIndex))
        {
            return;
        }

        editor.ReplaceNode(declaration, (current, _) => ModifierRemoval.RemoveAt((MemberDeclarationSyntax)current, modifierIndex));
    }

    /// <summary>Removes the reported modifier token from the declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The current syntax root.</param>
    /// <param name="declaration">The declaration that owns the redundant modifier.</param>
    /// <param name="token">The modifier token to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Document RemoveModifier(Document document, SyntaxNode root, MemberDeclarationSyntax declaration, SyntaxToken token)
    {
        var updated = ModifierRemoval.RemoveAt(declaration, declaration.Modifiers.IndexOf(token));
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated));
    }
}
