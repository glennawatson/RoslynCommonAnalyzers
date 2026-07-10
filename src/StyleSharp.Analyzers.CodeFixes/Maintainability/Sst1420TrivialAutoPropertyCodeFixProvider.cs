// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Converts an SST1420 property to an auto-property and removes its backing field.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1420TrivialAutoPropertyCodeFixProvider))]
[Shared]
public sealed class Sst1420TrivialAutoPropertyCodeFixProvider : CodeFixProvider
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
                || !Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out var fieldName)
                || !FieldReferenceAnalysis.TryFindSingleUseBackingField(
                    model,
                    property,
                    fieldName!,
                    context.CancellationToken,
                    out _,
                    out _,
                    out _))
                {
                    continue;
                }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Convert to auto-property",
                    cancellationToken => ApplyAsync(context.Document, root, model, property, fieldName!, cancellationToken),
                    equivalenceKey: nameof(Sst1420TrivialAutoPropertyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Applies the auto-property fix to the reported property.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property to rewrite.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document, or the original document when the property no longer qualifies.</returns>
    internal static Task<Document> ApplyAsync(
        Document document,
        SyntaxNode root,
        SemanticModel model,
        PropertyDeclarationSyntax property,
        CancellationToken cancellationToken)
        => Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out var fieldName)
            ? ApplyAsync(document, root, model, property, fieldName!, cancellationToken)
            : Task.FromResult(document);

    /// <summary>Applies the auto-property fix using a precomputed backing-field name.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property to rewrite.</param>
    /// <param name="fieldName">The expected backing-field name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document, or the original document when the property no longer qualifies.</returns>
    internal static Task<Document> ApplyAsync(
        Document document,
        SyntaxNode root,
        SemanticModel model,
        PropertyDeclarationSyntax property,
        string fieldName,
        CancellationToken cancellationToken)
    {
        if (!FieldReferenceAnalysis.TryFindSingleUseBackingField(
            model,
            property,
            fieldName,
            cancellationToken,
            out var field,
            out var variable,
            out _))
        {
            return Task.FromResult(document);
        }

        return Task.FromResult(Apply(document, root, property, field!, variable!));
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
        // An accessor list already carries the property's trailing newline on its closing brace, so that
        // common path is left untouched. A synthesized list has none, and an initializer pushes the
        // newline onto the trailing semicolon instead.
        var updated = property.AccessorList is { } accessorList
            ? property.WithAccessorList(accessorList.WithAccessors(ToAutoAccessors(accessorList.Accessors)))
            : property
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithAccessorList(CreateGetOnlyAccessorList().WithTrailingTrivia(property.GetTrailingTrivia()));

        if (variable.Initializer is { } initializer)
        {
            updated = updated
                .WithAccessorList(updated.AccessorList!.WithTrailingTrivia(SyntaxFactory.Space))
                .WithInitializer(initializer)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(property.GetTrailingTrivia()));
        }

        var annotation = new SyntaxAnnotation();
        updated = updated.WithAdditionalAnnotations(annotation);
        var changed = root.TrackNodes(property, field);
        var trackedProperty = changed.GetCurrentNode(property)!;
        changed = changed.ReplaceNode(trackedProperty, updated);
        var trackedField = changed.GetCurrentNode(field)!;
        changed = changed.RemoveNode(trackedField, SyntaxRemoveOptions.KeepNoTrivia)!;
        var currentProperty = CodeFixTriviaHelper.GetSingleAnnotatedProperty(changed, annotation);
        var previousToken = currentProperty.GetFirstToken().GetPreviousToken();
        var leadingTrivia = previousToken.TrailingTrivia.AddRange(currentProperty.GetLeadingTrivia());
        changed = changed.ReplaceToken(previousToken, previousToken.WithTrailingTrivia(default(SyntaxTriviaList)));
        currentProperty = CodeFixTriviaHelper.GetSingleAnnotatedProperty(changed, annotation);
        var normalizedProperty = currentProperty.WithLeadingTrivia(CodeFixTriviaHelper.CollapseLeadingBlankLine(leadingTrivia));
        return document.WithSyntaxRoot(changed.ReplaceNode(currentProperty, normalizedProperty));
    }

    /// <summary>Strips every accessor down to its auto-implemented semicolon form.</summary>
    /// <param name="accessors">The accessors to rewrite.</param>
    /// <returns>The auto-implemented accessors.</returns>
    private static SyntaxList<AccessorDeclarationSyntax> ToAutoAccessors(SyntaxList<AccessorDeclarationSyntax> accessors)
    {
        var rewritten = new AccessorDeclarationSyntax[accessors.Count];
        for (var i = 0; i < accessors.Count; i++)
        {
            rewritten[i] = accessors[i]
                .WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        return SyntaxFactory.List(rewritten);
    }

    /// <summary>Builds the <c>{ get; }</c> accessor list that replaces an expression-bodied property.</summary>
    /// <returns>The get-only accessor list.</returns>
    /// <remarks>
    /// The braces and semicolon carry their spacing directly rather than relying on a formatting pass,
    /// so the fix stays a pure syntax rewrite.
    /// </remarks>
    private static AccessorListSyntax CreateGetOnlyAccessorList()
        => SyntaxFactory.AccessorList(
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.SingletonList(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(SyntaxFactory.Space))),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken));
}
