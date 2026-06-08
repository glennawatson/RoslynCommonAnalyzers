// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Adds <c>readonly</c> to a field reported by SST1424.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1424FieldShouldBeReadonlyCodeFixProvider))]
[Shared]
public sealed class Sst1424FieldShouldBeReadonlyCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.FieldShouldBeReadonly.Id);

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

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<FieldDeclarationSyntax>() is not { } field)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make field readonly",
                    cancellationToken => Task.FromResult(AddReadonly(context.Document, root, field)),
                    equivalenceKey: nameof(Sst1424FieldShouldBeReadonlyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Adds the readonly modifier to the field declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The current syntax root.</param>
    /// <param name="field">The field declaration to update.</param>
    /// <returns>The updated document.</returns>
    internal static Document AddReadonly(Document document, SyntaxNode root, FieldDeclarationSyntax field)
    {
        var updated = field.WithModifiers(field.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
        return document.WithSyntaxRoot(root.ReplaceNode(field, updated));
    }
}
