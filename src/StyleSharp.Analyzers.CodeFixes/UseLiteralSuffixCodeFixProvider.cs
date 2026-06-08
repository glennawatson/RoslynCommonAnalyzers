// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a numeric literal cast with the equivalent literal suffix (SST1139).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseLiteralSuffixCodeFixProvider))]
[Shared]
public sealed class UseLiteralSuffixCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseLiteralSuffix.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not CastExpressionSyntax cast
                || UseLiteralSuffixAnalyzer.SuffixFor(cast) is not { } suffix)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use '{suffix}' suffix",
                    _ => Task.FromResult(Replace(context.Document, root, cast, suffix)),
                    equivalenceKey: nameof(UseLiteralSuffixCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the cast with the suffixed literal.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="cast">The cast expression.</param>
    /// <param name="suffix">The literal suffix to apply.</param>
    /// <returns>The updated document.</returns>
    internal static Document Replace(Document document, SyntaxNode root, CastExpressionSyntax cast, string suffix)
    {
        var literal = (LiteralExpressionSyntax)UseLiteralSuffixAnalyzer.Unwrap(cast.Expression);
        var suffixed = SyntaxFactory.ParseExpression(literal.Token.Text + suffix).WithTriviaFrom(cast);
        return document.WithSyntaxRoot(root.ReplaceNode(cast, suffixed));
    }
}
