// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Wraps the unbraced child statement of a control-flow statement in braces (SST1503).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1503RequireBracesCodeFixProvider))]
[Shared]
public sealed class Sst1503RequireBracesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.BracesRequired.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not { } control
                || !LayoutHelpers.TryGetEmbeddedStatement(control, out var child)
                || child is BlockSyntax)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add braces",
                    cancellationToken => WrapAsync(context.Document, child, cancellationToken),
                    equivalenceKey: nameof(Sst1503RequireBracesCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Wraps the embedded statement in braces.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="statement">The embedded statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> WrapAsync(Document document, StatementSyntax statement, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<TextChange>(2);
        LayoutFixHelpers.AppendBraceWrap(text, statement, LayoutFixHelpers.DetectNewLine(text), changes);
        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }
}
