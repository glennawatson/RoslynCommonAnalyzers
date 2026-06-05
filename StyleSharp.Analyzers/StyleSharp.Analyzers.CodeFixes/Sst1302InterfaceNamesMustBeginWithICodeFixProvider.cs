// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Rename;

namespace StyleSharp.Analyzers;

/// <summary>Code fix that renames an interface to add the leading <c>I</c> prefix.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1302InterfaceNamesMustBeginWithICodeFixProvider))]
[Shared]
public sealed class Sst1302InterfaceNamesMustBeginWithICodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1302InterfaceNamesMustBeginWithIAnalyzer.DiagnosticId);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not InterfaceDeclarationSyntax declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add 'I' prefix",
                    cancellationToken => AddPrefixAsync(context.Document, declaration, cancellationToken),
                    equivalenceKey: nameof(Sst1302InterfaceNamesMustBeginWithICodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Renames the interface symbol so its name gains a leading 'I', updating all references.</summary>
    /// <param name="document">The document containing the interface declaration.</param>
    /// <param name="declaration">The interface declaration to rename.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated solution.</returns>
    private static async Task<Solution> AddPrefixAsync(Document document, InterfaceDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model?.GetDeclaredSymbol(declaration, cancellationToken) is not { } symbol)
        {
            return solution;
        }

        return await Renamer
            .RenameSymbolAsync(solution, symbol, default, "I" + symbol.Name, cancellationToken)
            .ConfigureAwait(false);
    }
}
