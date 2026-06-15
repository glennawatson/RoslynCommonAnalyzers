// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant public, parameterless, empty constructor (SST1433).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantConstructorCodeFixProvider))]
[Shared]
public sealed class RedundantConstructorCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoRedundantConstructor.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is not { } constructor)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant constructor",
                    _ => Task.FromResult(Apply(context.Document, root, constructor)),
                    equivalenceKey: nameof(RedundantConstructorCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is not { } constructor)
        {
            return;
        }

        editor.RemoveNode(constructor, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <summary>Removes the redundant constructor so the compiler supplies the default.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="constructor">The redundant constructor.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ConstructorDeclarationSyntax constructor)
    {
        var updated = root.RemoveNode(constructor, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(updated!);
    }
}
