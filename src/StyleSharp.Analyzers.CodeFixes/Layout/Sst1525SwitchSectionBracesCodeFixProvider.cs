// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Wraps the body of a multi-statement switch section in braces on their own lines (SST1525).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1525SwitchSectionBracesCodeFixProvider))]
[Shared]
public sealed class Sst1525SwitchSectionBracesCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.SwitchSectionBraces.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

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
            if (!TryGetSection(root, diagnostic, out var section))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add braces",
                    cancellationToken => WrapAsync(context.Document, section, cancellationToken),
                    equivalenceKey: nameof(Sst1525SwitchSectionBracesCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryGetSection(root, diagnostic, out var section))
        {
            return;
        }

        LayoutFixHelpers.TryAppendSwitchSectionBraceWrap(
            text,
            section.Statements[0],
            section.Statements[section.Statements.Count - 1],
            LayoutFixHelpers.DetectNewLine(text),
            changes);
    }

    /// <summary>Wraps the section body in braces.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="section">The switch section.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> WrapAsync(Document document, SwitchSectionSyntax section, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<TextChange>(2);
        return LayoutFixHelpers.TryAppendSwitchSectionBraceWrap(
            text,
            section.Statements[0],
            section.Statements[section.Statements.Count - 1],
            LayoutFixHelpers.DetectNewLine(text),
            changes)
            ? document.WithText(text.WithChanges(changes))
            : document;
    }

    /// <summary>Resolves the multi-statement switch section the diagnostic marks.</summary>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="section">The resolved section.</param>
    /// <returns><see langword="true"/> when a multi-statement section is found.</returns>
    private static bool TryGetSection(SyntaxNode root, Diagnostic diagnostic, out SwitchSectionSyntax section)
    {
        section = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<SwitchSectionSyntax>()!;
        return section is { Statements.Count: > 1 };
    }
}
