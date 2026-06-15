// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes the modifier reported by SST1419 (redundant) or SST1427 (<c>protected</c> in a sealed type).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveModifierCodeFixProvider))]
[Shared]
public sealed class RemoveModifierCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.NoRedundantModifier.Id,
        MaintainabilityRules.NoProtectedInSealed.Id);

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

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } declaration)
            {
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

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var token = editor.OriginalRoot.FindToken(diagnostic.Location.SourceSpan.Start);
        if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } declaration)
        {
            return;
        }

        // Removing the first modifier would otherwise drop the declaration's leading indentation, so
        // carry it over. For a non-leading modifier this is a no-op.
        var updated = declaration
            .WithModifiers(declaration.Modifiers.Remove(token))
            .WithLeadingTrivia(declaration.GetLeadingTrivia());
        editor.ReplaceNode(declaration, updated);
    }

    /// <summary>Removes the reported modifier token from the declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The current syntax root.</param>
    /// <param name="declaration">The declaration that owns the redundant modifier.</param>
    /// <param name="token">The modifier token to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Document RemoveModifier(Document document, SyntaxNode root, MemberDeclarationSyntax declaration, SyntaxToken token)
    {
        // Removing the first modifier would otherwise drop the declaration's leading indentation, so
        // carry it over. For a non-leading modifier this is a no-op.
        var updated = declaration
            .WithModifiers(declaration.Modifiers.Remove(token))
            .WithLeadingTrivia(declaration.GetLeadingTrivia());
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated));
    }
}
