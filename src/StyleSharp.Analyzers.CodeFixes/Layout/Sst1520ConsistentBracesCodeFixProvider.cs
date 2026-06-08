// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Adds braces to every bare clause of an inconsistent if/else chain (SST1520).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1520ConsistentBracesCodeFixProvider))]
[Shared]
public sealed class Sst1520ConsistentBracesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.BracesUsedConsistently.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not IfStatementSyntax ifStatement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add braces to all clauses",
                    cancellationToken => WrapChainAsync(context.Document, ifStatement, cancellationToken),
                    equivalenceKey: nameof(Sst1520ConsistentBracesCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Wraps every bare clause body in the if/else chain in braces.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="ifStatement">The top of the if/else chain.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> WrapChainAsync(Document document, IfStatementSyntax ifStatement, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var changes = new List<TextChange>(CountClauses(ifStatement) * 2);

        var current = ifStatement;
        while (true)
        {
            WrapIfBare(text, current.Statement, newLine, changes);
            if (current.Else is not { } elseClause)
            {
                break;
            }

            if (elseClause.Statement is IfStatementSyntax elseIf)
            {
                current = elseIf;
                continue;
            }

            WrapIfBare(text, elseClause.Statement, newLine, changes);
            break;
        }

        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Counts the clauses in an <c>if</c>/<c>else if</c>/<c>else</c> chain.</summary>
    /// <param name="ifStatement">The top of the chain.</param>
    /// <returns>The number of clause bodies that may need wrapping.</returns>
    private static int CountClauses(IfStatementSyntax ifStatement)
    {
        var count = 1;
        var current = ifStatement;
        while (current.Else is { } elseClause)
        {
            count++;
            if (elseClause.Statement is not IfStatementSyntax elseIf)
            {
                break;
            }

            current = elseIf;
        }

        return count;
    }

    /// <summary>Wraps a clause body in braces when it is not already a block.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="statement">The clause body.</param>
    /// <param name="newLine">The document newline sequence.</param>
    /// <param name="changes">The change set to append to.</param>
    private static void WrapIfBare(SourceText text, StatementSyntax statement, string newLine, List<TextChange> changes)
    {
        if (statement is BlockSyntax)
        {
            return;
        }

        LayoutFixHelpers.AppendBraceWrap(text, statement, newLine, changes);
    }
}
