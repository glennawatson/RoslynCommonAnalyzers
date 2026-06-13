// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes an unnecessary cast (SST1175), keeping the operand expression.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantCastCodeFixProvider))]
[Shared]
public sealed class RedundantCastCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantCast.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<CastExpressionSyntax>() is not { } cast)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the unnecessary cast",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, cast)),
                    equivalenceKey: nameof(RedundantCastCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the cast expression with its operand.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="cast">The redundant cast expression.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, CastExpressionSyntax cast)
    {
        var replacement = cast.Expression.WithTriviaFrom(cast);
        return document.WithSyntaxRoot(root.ReplaceNode(cast, replacement));
    }
}
