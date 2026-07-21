// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a construction-only private setter, collapsing <c>get; private set;</c> to a get-only <c>get;</c>
/// auto-property (SST2332). Any initializer the property carries is kept, and the compiler now rejects a stray
/// later assignment instead of allowing it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2332PrivateSetterOnlyWrittenDuringConstructionCodeFixProvider))]
[Shared]
public sealed class Sst2332PrivateSetterOnlyWrittenDuringConstructionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DesignRules.PrivateSetterOnlyWrittenDuringConstruction.Id);

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
            if (Resolve(root, diagnostic) is not var (property, updated))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make the property get-only",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(property, updated))),
                    equivalenceKey: nameof(Sst2332PrivateSetterOnlyWrittenDuringConstructionCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not var (property, updated))
        {
            return;
        }

        editor.ReplaceNode(property, updated);
    }

    /// <summary>Resolves the diagnostic to the property and its get-only rewrite.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The property and its rewrite, or <see langword="null"/> when the shape no longer matches.</returns>
    private static (PropertyDeclarationSyntax Property, PropertyDeclarationSyntax Updated)? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<PropertyDeclarationSyntax>() is not { AccessorList: { } accessors } property
            || FindGetter(accessors) is not { } getter)
        {
            return null;
        }

        var updated = property.WithAccessorList(accessors.WithAccessors(SyntaxFactory.SingletonList(getter)));
        return (property, updated);
    }

    /// <summary>Finds the get accessor in an accessor list.</summary>
    /// <param name="accessors">The accessor list.</param>
    /// <returns>The get accessor, or <see langword="null"/> when there is none.</returns>
    private static AccessorDeclarationSyntax? FindGetter(AccessorListSyntax accessors)
    {
        for (var i = 0; i < accessors.Accessors.Count; i++)
        {
            if (accessors.Accessors[i].IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                return accessors.Accessors[i];
            }
        }

        return null;
    }
}
