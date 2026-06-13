// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces <c>default(T)</c> with the bare <c>default</c> literal (SST1188).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseDefaultLiteralCodeFixProvider))]
[Shared]
public sealed class UseDefaultLiteralCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseDefaultLiteral.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not DefaultExpressionSyntax defaultExpression)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the 'default' literal",
                    _ => Task.FromResult(Apply(context.Document, root, defaultExpression)),
                    equivalenceKey: nameof(UseDefaultLiteralCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Swaps the explicit <c>default(T)</c> for the target-typed <c>default</c> literal.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="defaultExpression">The explicit default expression.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, DefaultExpressionSyntax defaultExpression)
    {
        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword))
            .WithTriviaFrom(defaultExpression);

        return document.WithSyntaxRoot(root.ReplaceNode(defaultExpression, literal));
    }
}
