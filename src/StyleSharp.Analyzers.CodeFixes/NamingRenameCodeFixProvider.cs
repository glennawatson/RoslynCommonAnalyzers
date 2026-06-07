// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Rename;

namespace StyleSharp.Analyzers;

/// <summary>
/// A single rename code fix shared by every naming (SST13xx) rule. Each analyzer
/// records the suggested replacement name in the diagnostic's properties under
/// <see cref="NamingDiagnostic.NewNameKey"/>; this fix reads it and renames the
/// declared symbol across the whole solution.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NamingRenameCodeFixProvider))]
[Shared]
public sealed class NamingRenameCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => NamingRules.AllFixableIds;

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
            if (!diagnostic.Properties.TryGetValue(NamingDiagnostic.NewNameKey, out var newName) || string.IsNullOrEmpty(newName))
            {
                continue;
            }

            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename to '{newName}'",
                    cancellationToken => RenameAsync(context.Document, node, newName!, cancellationToken),
                    equivalenceKey: nameof(NamingRenameCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Renames the declared symbol at <paramref name="node"/> to <paramref name="newName"/> across the solution.</summary>
    /// <param name="document">The document containing the declaration.</param>
    /// <param name="node">The declaration node whose symbol should be renamed.</param>
    /// <param name="newName">The replacement name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated solution.</returns>
    private static async Task<Solution> RenameAsync(Document document, SyntaxNode node, string newName, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        return model?.GetDeclaredSymbol(node, cancellationToken) is not { } symbol
            ? solution
            : await Renamer
                .RenameSymbolAsync(solution, symbol, default, newName, cancellationToken)
                .ConfigureAwait(false);
    }
}
