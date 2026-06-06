// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a parameter-naming string literal with a <c>nameof</c> expression (SST1415).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseNameofCodeFixProvider))]
[Shared]
public sealed class UseNameofCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UseNameofForParameter.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not LiteralExpressionSyntax literal)
            {
                continue;
            }

            var nameofExpression = SyntaxFactory.ParseExpression($"nameof({literal.Token.ValueText})").WithTriviaFrom(literal);

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use 'nameof({literal.Token.ValueText})'",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(literal, nameofExpression))),
                    equivalenceKey: nameof(UseNameofCodeFixProvider)),
                diagnostic);
        }
    }
}
