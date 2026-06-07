// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Rewrites a using directive's name in fully qualified form (SST1135).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UsingDirectiveQualifiedCodeFixProvider))]
[Shared]
public sealed class UsingDirectiveQualifiedCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UsingDirectiveQualified.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not NameSyntax name)
            {
                continue;
            }

            var symbol = model.GetSymbolInfo(name, context.CancellationToken).Symbol;
            if (symbol is not (INamespaceSymbol or INamedTypeSymbol))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Qualify the using directive",
                    _ => Task.FromResult(Replace(context.Document, root, name, symbol)),
                    equivalenceKey: nameof(UsingDirectiveQualifiedCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the name with its fully qualified form.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="name">The using directive name.</param>
    /// <param name="symbol">The resolved namespace or type symbol.</param>
    /// <returns>The updated document.</returns>
    private static Document Replace(Document document, SyntaxNode root, NameSyntax name, ISymbol symbol)
    {
        var qualified = SyntaxFactory.ParseName(UsingDirectiveQualifiedAnalyzer.QualifiedName(symbol)).WithTriviaFrom(name);
        return document.WithSyntaxRoot(root.ReplaceNode(name, qualified));
    }
}
