// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a framework type name with its built-in keyword alias (SST1121).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BuiltInTypeAliasCodeFixProvider))]
[Shared]
public sealed class BuiltInTypeAliasCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseBuiltInTypeAlias.Id);

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

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (model.GetSymbolInfo(node, context.CancellationToken).Symbol is not INamedTypeSymbol type
                || BuiltInTypeAliases.Keyword(type.SpecialType) is not { } keyword)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use '{keyword}'",
                    _ => Task.FromResult(Replace(context.Document, root, node, keyword)),
                    equivalenceKey: nameof(BuiltInTypeAliasCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the type node with a predefined-type keyword.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="node">The framework type node.</param>
    /// <param name="keyword">The keyword alias.</param>
    /// <returns>The updated document.</returns>
    private static Document Replace(Document document, SyntaxNode root, SyntaxNode node, string keyword)
    {
        var predefined = SyntaxFactory.PredefinedType(SyntaxFactory.Token(BuiltInTypeAliases.TokenKind(keyword))).WithTriviaFrom(node);
        return document.WithSyntaxRoot(root.ReplaceNode(node, predefined));
    }
}
