// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a modifier that restates the declaration's default (SST1491).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1491RedundantModifierCodeFixProvider))]
[Shared]
public sealed class Sst1491RedundantModifierCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.RedundantModifier.Id);

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

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!ReportedModifier.TryFind(root, diagnostic, out var declaration, out var token, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Remove the redundant '{token.ValueText}' modifier",
                    _ => Task.FromResult(RemoveModifierCodeFixProvider.RemoveModifier(context.Document, root, declaration, token)),
                    equivalenceKey: nameof(Sst1491RedundantModifierCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!ReportedModifier.TryFind(editor.OriginalRoot, diagnostic, out var declaration, out var token, out _))
        {
            return;
        }

        // One interface member can carry two redundant modifiers, and the second edit runs against a list the
        // first has already shortened — so the modifier is found by kind in the current node rather than by
        // its index in the original. A modifier kind cannot repeat within one list, so the match is exact.
        var kind = token.Kind();
        editor.ReplaceNode(declaration, (current, _) => ModifierRemoval.RemoveKind((MemberDeclarationSyntax)current, kind));
    }
}
