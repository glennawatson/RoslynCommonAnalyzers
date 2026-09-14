// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Registers and batch-applies code fixes that rewrite a type declaration with a value the analyzer stored on the diagnostic.</summary>
internal static class TypeDeclarationValueCodeFix
{
    /// <summary>Creates the fix-all provider that rewrites each resolved declaration as earlier edits in the batch left it.</summary>
    /// <param name="resolve">Resolves a diagnostic's declaration and value, or <see langword="null"/> when no fix is offered.</param>
    /// <param name="rewrite">Rewrites a declaration with the value.</param>
    /// <returns>The fix-all provider.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BatchEditFixAllProvider CreateFixAll(
        Func<SyntaxNode, Diagnostic, TypeDeclarationFix?> resolve,
        Func<TypeDeclarationSyntax, string, TypeDeclarationSyntax> rewrite) =>
        new((editor, diagnostic) => ApplyBatchEdit(editor, diagnostic, resolve, rewrite));

    /// <summary>Registers one code action per diagnostic whose declaration and value resolve.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">Builds the code action title from the resolved value.</param>
    /// <param name="equivalenceKey">The equivalence key grouping the fix across documents.</param>
    /// <param name="resolve">Resolves a diagnostic's declaration and value, or <see langword="null"/> when no fix is offered.</param>
    /// <param name="rewrite">Rewrites a declaration with the value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        Func<string, string> title,
        string equivalenceKey,
        Func<SyntaxNode, Diagnostic, TypeDeclarationFix?> resolve,
        Func<TypeDeclarationSyntax, string, TypeDeclarationSyntax> rewrite)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (resolve(root, diagnostic) is not var (declaration, value))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title(value),
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, rewrite(declaration, value)))),
                    equivalenceKey),
                diagnostic);
        }
    }

    /// <summary>Queues one diagnostic's rewrite against the declaration as earlier edits in the batch left it.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="resolve">Resolves the diagnostic's declaration and value.</param>
    /// <param name="rewrite">Rewrites a declaration with the value.</param>
    private static void ApplyBatchEdit(
        DocumentEditor editor,
        Diagnostic diagnostic,
        Func<SyntaxNode, Diagnostic, TypeDeclarationFix?> resolve,
        Func<TypeDeclarationSyntax, string, TypeDeclarationSyntax> rewrite)
    {
        if (resolve(editor.OriginalRoot, diagnostic) is not var (declaration, value))
        {
            return;
        }

        editor.ReplaceNode(declaration, (current, _) => rewrite((TypeDeclarationSyntax)current, value));
    }
}
