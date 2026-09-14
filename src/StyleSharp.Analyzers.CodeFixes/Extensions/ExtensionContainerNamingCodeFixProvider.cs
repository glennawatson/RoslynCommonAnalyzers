// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Renames an SST1704 extension container to the configured preferred suffix.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExtensionContainerNamingCodeFixProvider))]
[Shared]
public sealed class ExtensionContainerNamingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ExtensionRules.ExtensionContainerNaming.Id);

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

        var tree = await context.Document.GetSyntaxTreeAsync(context.CancellationToken).ConfigureAwait(false);
        if (tree is null)
        {
            return;
        }

        var options = context.Document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree);
        var preferredSuffix = ExtensionContainerNaming.ReadPreferredSuffix(options);

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is not ClassDeclarationSyntax declaration)
            {
                continue;
            }

            var newName = ExtensionContainerNaming.BuildPreferredName(declaration.Identifier.ValueText, preferredSuffix);
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename to '{newName}'",
                    cancellationToken => NamingRenameCodeFixProvider.RenameAsync(context.Document, declaration, newName, cancellationToken),
                    equivalenceKey: nameof(ExtensionContainerNamingCodeFixProvider)),
                diagnostic);
        }
    }
}
