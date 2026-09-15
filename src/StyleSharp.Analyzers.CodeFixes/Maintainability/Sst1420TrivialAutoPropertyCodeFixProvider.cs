// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        BackingFieldPropertyCodeFix.RegisterAsync(
            context,
            "Convert to auto-property",
            nameof(Sst1420TrivialAutoPropertyCodeFixProvider),
            static (model, property, cancellationToken) =>
                Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out var fieldName)
                && FieldReferenceAnalysis.TryFindSingleUseBackingField(model, property, fieldName!, cancellationToken, out _, out _, out _)
                    ? fieldName
                    : null,
            ApplyAsync);

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
        CancellationToken cancellationToken) =>
        Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out var fieldName)
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
        CancellationToken cancellationToken) => !FieldReferenceAnalysis.TryFindSingleUseBackingField(
            model,
            property,
            fieldName,
            cancellationToken,
            out var field,
            out var variable,
            out _)
            ? Task.FromResult(document)
            : Task.FromResult(Apply(document, root, property, field!, variable!));

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
            : property.Update(
                property.AttributeLists,
                property.Modifiers,
                property.Type,
                property.ExplicitInterfaceSpecifier,
                property.Identifier,
                CreateGetOnlyAccessorList().WithTrailingTrivia(property.GetTrailingTrivia()),
                expressionBody: null,
                property.Initializer,
                semicolonToken: default);

        if (variable.Initializer is { } initializer)
        {
            updated = updated.Update(
                updated.AttributeLists,
                updated.Modifiers,
                updated.Type,
                updated.ExplicitInterfaceSpecifier,
                updated.Identifier,
                updated.AccessorList!.WithTrailingTrivia(SyntaxFactory.Space),
                updated.ExpressionBody,
                initializer,
                SyntaxFactory.Token(default, SyntaxKind.SemicolonToken, property.GetTrailingTrivia()));
        }

        return document.WithSyntaxRoot(CodeFixTriviaHelper.ReplacePropertyRemovingField(root, property, updated, field));
    }

    /// <summary>Strips every accessor down to its auto-implemented semicolon form.</summary>
    /// <param name="accessors">The accessors to rewrite.</param>
    /// <returns>The auto-implemented accessors.</returns>
    private static SyntaxList<AccessorDeclarationSyntax> ToAutoAccessors(SyntaxList<AccessorDeclarationSyntax> accessors)
    {
        var rewritten = new AccessorDeclarationSyntax[accessors.Count];
        for (var i = 0; i < accessors.Count; i++)
        {
            var accessor = accessors[i];
            rewritten[i] = accessor.Update(
                accessor.AttributeLists,
                accessor.Modifiers,
                accessor.Keyword,
                body: null,
                expressionBody: null,
                SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        return SyntaxFactory.List(rewritten);
    }

    /// <summary>Builds the <c>{ get; }</c> accessor list that replaces an expression-bodied property.</summary>
    /// <returns>The get-only accessor list.</returns>
    /// <remarks>
    /// The braces and semicolon carry their spacing directly rather than relying on a formatting pass,
    /// so the fix stays a pure syntax rewrite.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AccessorListSyntax CreateGetOnlyAccessorList() =>
        SyntaxFactory.AccessorList(
            SyntaxFactory.Token(default, SyntaxKind.OpenBraceToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            SyntaxFactory.SingletonList(
                SyntaxFactory.AccessorDeclaration(
                    SyntaxKind.GetAccessorDeclaration,
                    attributeLists: default,
                    modifiers: default,
                    SyntaxFactory.Token(SyntaxKind.GetKeyword),
                    body: null,
                    expressionBody: null,
                    SyntaxFactory.Token(default, SyntaxKind.SemicolonToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)))),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken));
}
