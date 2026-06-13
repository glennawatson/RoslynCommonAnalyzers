// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes an empty finalizer (SST1434).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyFinalizerCodeFixProvider))]
[Shared]
public sealed class EmptyFinalizerCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoEmptyFinalizer.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<DestructorDeclarationSyntax>() is not { } finalizer)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the empty finalizer",
                    _ => Task.FromResult(Apply(context.Document, root, finalizer)),
                    equivalenceKey: nameof(EmptyFinalizerCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Removes the empty finalizer.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="finalizer">The empty finalizer.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, DestructorDeclarationSyntax finalizer)
    {
        var updated = root.RemoveNode(finalizer, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(updated!);
    }
}
