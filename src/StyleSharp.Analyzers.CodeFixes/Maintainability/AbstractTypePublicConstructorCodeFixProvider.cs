// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Changes a <c>public</c> constructor on an abstract type to <c>protected</c> (SST1428).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AbstractTypePublicConstructorCodeFixProvider))]
[Shared]
public sealed class AbstractTypePublicConstructorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.NoPublicConstructorOnAbstractType.Id);

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
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (!token.IsKind(SyntaxKind.PublicKeyword))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make the constructor 'protected'",
                    _ => Task.FromResult(Apply(context.Document, root, token)),
                    equivalenceKey: nameof(AbstractTypePublicConstructorCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Swaps the <c>public</c> keyword for <c>protected</c>, keeping its trivia.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="publicKeyword">The <c>public</c> modifier token.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SyntaxToken publicKeyword)
    {
        var protectedKeyword = SyntaxFactory.Token(publicKeyword.LeadingTrivia, SyntaxKind.ProtectedKeyword, publicKeyword.TrailingTrivia);
        return document.WithSyntaxRoot(root.ReplaceToken(publicKeyword, protectedKeyword));
    }
}
