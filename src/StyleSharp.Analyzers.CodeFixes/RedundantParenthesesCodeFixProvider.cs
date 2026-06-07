// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes an empty anonymous-method parameter list (SST1410) or attribute argument list (SST1411).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantParenthesesCodeFixProvider))]
[Shared]
public sealed class RedundantParenthesesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.RemoveDelegateParentheses.Id,
        MaintainabilityRules.RemoveAttributeParentheses.Id);

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
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var rewritten = Rewrite(node);
            if (rewritten is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove empty parentheses",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(rewritten.Value.Original, rewritten.Value.Replacement))),
                    equivalenceKey: nameof(RedundantParenthesesCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Computes the node replacement that drops the empty parentheses, or <see langword="null"/>.</summary>
    /// <param name="node">The node at the diagnostic location.</param>
    /// <returns>The original and replacement nodes, or <see langword="null"/>.</returns>
    private static (SyntaxNode Original, SyntaxNode Replacement)? Rewrite(SyntaxNode node)
    {
        var anonymous = node.FirstAncestorOrSelf<AnonymousMethodExpressionSyntax>();
        if (anonymous?.ParameterList is not null)
        {
            var updated = anonymous
                .WithParameterList(null)
                .WithDelegateKeyword(anonymous.DelegateKeyword.WithTrailingTrivia(SyntaxFactory.Space));
            return (anonymous, updated);
        }

        var attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
        return attribute?.ArgumentList is null ? null : (attribute, attribute.WithArgumentList(null));
    }
}
