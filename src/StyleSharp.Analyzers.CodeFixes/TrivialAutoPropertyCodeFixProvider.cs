// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Converts an SST1420 property to an auto-property and removes its backing field.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TrivialAutoPropertyCodeFixProvider))]
[Shared]
public sealed class TrivialAutoPropertyCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.PreferAutoProperty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is not { } property
                || !FieldReferenceAnalysis.TryFindSingleUseBackingField(
                    model,
                    property,
                    context.CancellationToken,
                    out var field,
                    out var variable,
                    out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Convert to auto-property",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, property, field!, variable!)),
                    equivalenceKey: nameof(TrivialAutoPropertyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Rewrites the property and removes its backing-field declaration.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="property">The property.</param>
    /// <param name="field">The backing-field declaration.</param>
    /// <param name="variable">The backing-field variable.</param>
    /// <returns>The updated document.</returns>
    private static Document Apply(
        Document document,
        SyntaxNode root,
        PropertyDeclarationSyntax property,
        FieldDeclarationSyntax field,
        VariableDeclaratorSyntax variable)
    {
        var accessors = property.AccessorList!.Accessors;
        var rewritten = new AccessorDeclarationSyntax[accessors.Count];
        for (var i = 0; i < accessors.Count; i++)
        {
            rewritten[i] = accessors[i]
                .WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        var updated = property.WithAccessorList(property.AccessorList.WithAccessors(SyntaxFactory.List(rewritten)));
        if (variable.Initializer is { } initializer)
        {
            updated = updated.WithInitializer(initializer).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        var annotation = new SyntaxAnnotation();
        updated = updated.WithAdditionalAnnotations(annotation);
        var changed = root.TrackNodes(property, field);
        var trackedProperty = changed.GetCurrentNode(property)!;
        changed = changed.ReplaceNode(trackedProperty, updated);
        var trackedField = changed.GetCurrentNode(field)!;
        changed = changed.RemoveNode(trackedField, SyntaxRemoveOptions.KeepNoTrivia)!;
        var currentProperty = changed.GetAnnotatedNodes(annotation).OfType<PropertyDeclarationSyntax>().Single();
        var previousToken = currentProperty.GetFirstToken().GetPreviousToken();
        var leadingTrivia = previousToken.TrailingTrivia.AddRange(currentProperty.GetLeadingTrivia());
        changed = changed.ReplaceToken(previousToken, previousToken.WithTrailingTrivia(default(SyntaxTriviaList)));
        currentProperty = changed.GetAnnotatedNodes(annotation).OfType<PropertyDeclarationSyntax>().Single();
        var normalizedProperty = currentProperty.WithLeadingTrivia(CodeFixTriviaHelper.CollapseLeadingBlankLine(leadingTrivia));
        return document.WithSyntaxRoot(changed.ReplaceNode(currentProperty, normalizedProperty));
    }
}
