// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant <c>default:</c> switch section that only breaks (SST1179).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantDefaultSwitchSectionCodeFixProvider))]
[Shared]
public sealed class RedundantDefaultSwitchSectionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantDefaultSwitchSection.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<SwitchSectionSyntax>() is not { } section)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant 'default' section",
                    _ => Task.FromResult(Apply(context.Document, root, section)),
                    equivalenceKey: nameof(RedundantDefaultSwitchSectionCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<SwitchSectionSyntax>() is not { } section)
        {
            return;
        }

        editor.RemoveNode(section, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <summary>Removes the redundant <c>default:</c> section from its switch.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="section">The redundant switch section.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SwitchSectionSyntax section)
    {
        var updated = root.RemoveNode(section, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(updated!);
    }
}
