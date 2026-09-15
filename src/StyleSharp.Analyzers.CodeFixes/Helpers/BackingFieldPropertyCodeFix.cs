// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Registers the code fixes that rewrite a property and delete the backing field it no longer needs.</summary>
internal static class BackingFieldPropertyCodeFix
{
    /// <summary>Registers the fix for every reported property whose backing field can still be removed.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="title">The code action title.</param>
    /// <param name="equivalenceKey">The code action equivalence key.</param>
    /// <param name="resolveFieldName">Returns the name of the backing field to remove, or <see langword="null"/> when the property no longer qualifies.</param>
    /// <param name="apply">Rewrites the property and removes the named backing field.</param>
    /// <returns>A task that completes once every applicable fix is registered.</returns>
    internal static async Task RegisterAsync(
        CodeFixContext context,
        string title,
        string equivalenceKey,
        Func<SemanticModel, PropertyDeclarationSyntax, CancellationToken, string?> resolveFieldName,
        Func<Document, SyntaxNode, SemanticModel, PropertyDeclarationSyntax, string, CancellationToken, Task<Document>> apply)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];

            // The backing field is deleted from the member list, so a directive among the members would
            // lose the half that sits on it.
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is not { } property
                || property.Parent is not TypeDeclarationSyntax containing
                || DirectiveBoundaries.SeparateMembers(containing)
                || resolveFieldName(model, property, context.CancellationToken) is not { } fieldName)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => apply(context.Document, root, model, property, fieldName, cancellationToken),
                    equivalenceKey),
                diagnostic);
        }
    }
}
