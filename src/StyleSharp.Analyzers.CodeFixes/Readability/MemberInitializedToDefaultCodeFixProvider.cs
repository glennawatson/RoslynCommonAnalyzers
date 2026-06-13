// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant initialization to a type's default value (SST1176).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberInitializedToDefaultCodeFixProvider))]
[Shared]
public sealed class MemberInitializedToDefaultCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoMemberInitializedToDefault.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<EqualsValueClauseSyntax>() is not { } initializer)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant initializer",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, initializer)),
                    equivalenceKey: nameof(MemberInitializedToDefaultCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Drops the initializer, handling the auto-property semicolon when present.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="initializer">The redundant initializer clause.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, EqualsValueClauseSyntax initializer)
    {
        // A property carries the initializer plus a trailing ';'; both must go. A field/event keeps
        // its own ';' on the declaration, so only the declarator's initializer is removed.
        if (initializer.Parent is PropertyDeclarationSyntax property)
        {
            var trimmed = property
                .WithInitializer(null)
                .WithSemicolonToken(default)
                .WithTrailingTrivia(property.GetTrailingTrivia());
            return document.WithSyntaxRoot(root.ReplaceNode(property, trimmed));
        }

        if (initializer.Parent is VariableDeclaratorSyntax declarator)
        {
            var trimmed = declarator.WithInitializer(null).WithTrailingTrivia(declarator.GetTrailingTrivia());
            return document.WithSyntaxRoot(root.ReplaceNode(declarator, trimmed));
        }

        return document;
    }
}
