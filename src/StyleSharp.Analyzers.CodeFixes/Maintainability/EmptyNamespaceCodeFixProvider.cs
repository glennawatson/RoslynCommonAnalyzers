// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes an empty namespace declaration (SST1435).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyNamespaceCodeFixProvider))]
[Shared]
public sealed class EmptyNamespaceCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoEmptyNamespace.Id);

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

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>() is not { } declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the empty namespace",
                    _ => Task.FromResult(Apply(context.Document, root, declaration)),
                    equivalenceKey: nameof(EmptyNamespaceCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>() is not { } declaration)
        {
            return;
        }

        editor.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <summary>Removes the empty namespace declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The empty namespace declaration.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BaseNamespaceDeclarationSyntax declaration)
    {
        var updated = root.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(updated!);
    }
}
