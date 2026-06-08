// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an empty statement (SST1106). The fix is only offered when the semicolon stands in a
/// block, switch section, or top-level statement list, where deleting it cannot change control
/// flow — an empty statement embedded as a loop or <c>if</c> body is left for manual review.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1106EmptyStatementCodeFixProvider))]
[Shared]
public sealed class Sst1106EmptyStatementCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.EmptyStatement.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not EmptyStatementSyntax { Parent: BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax } statement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove empty statement",
                    _ => RemoveAsync(context.Document, root, statement),
                    equivalenceKey: nameof(Sst1106EmptyStatementCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Removes the empty statement and its surrounding trivia.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="statement">The empty statement to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Task<Document> RemoveAsync(Document document, SyntaxNode root, SyntaxNode statement)
    {
        // A top-level statement is wrapped in a GlobalStatementSyntax; remove the wrapper too.
        var toRemove = statement.Parent is GlobalStatementSyntax global ? global : statement;
        var updated = root.RemoveNode(toRemove, SyntaxRemoveOptions.KeepNoTrivia);
        return Task.FromResult(updated is null ? document : document.WithSyntaxRoot(updated));
    }
}
