// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a single-use backing field with the C# 14 <c>field</c> keyword.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferFieldKeywordCodeFixProvider))]
[Shared]
public sealed class PreferFieldKeywordCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.PreferFieldKeyword.Id);

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
                    out var symbol))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the field keyword",
                    cancellationToken => MaterializeAsync(
                        context.Document,
                        Apply(root, model, property, field!, variable!, symbol!, cancellationToken),
                        cancellationToken),
                    equivalenceKey: nameof(PreferFieldKeywordCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Materializes a changed root using the most specific syntax API supported by the current Roslyn slot.</summary>
    /// <param name="document">The document to update.</param>
    /// <param name="changedRoot">The updated syntax root.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
#if ROSLYN_5_OR_GREATER
    private static Task<Document> MaterializeAsync(Document document, SyntaxNode changedRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(document.WithSyntaxRoot(changedRoot));
    }
#else
    private static async Task<Document> MaterializeAsync(Document document, SyntaxNode changedRoot, CancellationToken cancellationToken)
    {
        // Roslyn 4.x represents "field" as an identifier. Reparse through the host
        // document so a C# 14 host materializes FieldExpressionSyntax.
        var text = await changedRoot.SyntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text);
    }
#endif

    /// <summary>Rewrites property references and removes the explicit backing field.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property.</param>
    /// <param name="field">The backing-field declaration.</param>
    /// <param name="variable">The backing-field variable.</param>
    /// <param name="symbol">The backing-field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated syntax root.</returns>
    private static SyntaxNode Apply(
        SyntaxNode root,
        SemanticModel model,
        PropertyDeclarationSyntax property,
        FieldDeclarationSyntax field,
        VariableDeclaratorSyntax variable,
        IFieldSymbol symbol,
        CancellationToken cancellationToken)
    {
        var references = property.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(node => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(node, cancellationToken).Symbol, symbol));
        var updated = property.ReplaceNodes(
            references,
            (_, rewritten) => CreateFieldExpression().WithTriviaFrom(rewritten));

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
        return changed.ReplaceNode(currentProperty, normalizedProperty);
    }

    /// <summary>Creates the backing-field expression supported by the current Roslyn slot.</summary>
    /// <returns>An expression that writes as the contextual <c>field</c> keyword.</returns>
#if ROSLYN_5_OR_GREATER
    private static FieldExpressionSyntax CreateFieldExpression()
        => SyntaxFactory.FieldExpression(SyntaxFactory.Token(SyntaxKind.FieldKeyword));
#else
    private static IdentifierNameSyntax CreateFieldExpression()
        => SyntaxFactory.IdentifierName("field");
#endif
}
