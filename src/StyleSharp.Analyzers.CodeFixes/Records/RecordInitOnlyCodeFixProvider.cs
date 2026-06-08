// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a record property's <c>set</c> accessor with an <c>init</c> accessor (SST1802),
/// preserving its attributes, modifiers, body or expression body, and trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RecordInitOnlyCodeFixProvider))]
[Shared]
public sealed class RecordInitOnlyCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(RecordRules.InitOnlyProperty.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not AccessorDeclarationSyntax accessor)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use 'init' accessor",
                    cancellationToken => ConvertAsync(context.Document, accessor, cancellationToken),
                    equivalenceKey: nameof(RecordInitOnlyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the set accessor with an equivalent init accessor.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="accessor">The set accessor to convert.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> ConvertAsync(Document document, AccessorDeclarationSyntax accessor, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var initKeyword = SyntaxFactory.Token(accessor.Keyword.LeadingTrivia, SyntaxKind.InitKeyword, accessor.Keyword.TrailingTrivia);
        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithAttributeLists(accessor.AttributeLists)
            .WithModifiers(accessor.Modifiers)
            .WithKeyword(initKeyword)
            .WithBody(accessor.Body)
            .WithExpressionBody(accessor.ExpressionBody)
            .WithSemicolonToken(accessor.SemicolonToken);

        return document.WithSyntaxRoot(root!.ReplaceNode(accessor, initAccessor));
    }
}
