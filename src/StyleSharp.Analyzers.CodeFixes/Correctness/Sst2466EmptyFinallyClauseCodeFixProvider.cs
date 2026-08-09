// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an empty <c>finally</c> clause (SST2466). When the <c>try</c> has no <c>catch</c> either, the
/// whole statement goes and its body is lifted into the enclosing block, because a <c>try</c> with neither
/// handler nor cleanup is just its own statements.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2466EmptyFinallyClauseCodeFixProvider))]
[Shared]
public sealed class Sst2466EmptyFinallyClauseCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(CorrectnessRules.EmptyFinallyClause.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<TryStatementSyntax>() is not
                { Finally: not null } tryStatement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the empty finally clause",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(Rewrite(root, tryStatement))),
                    nameof(Sst2466EmptyFinallyClauseCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Removes the clause, and the whole <c>try</c> when nothing is left to handle anything.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="tryStatement">The try statement carrying the empty clause.</param>
    /// <returns>The rewritten root.</returns>
    private static SyntaxNode Rewrite(SyntaxNode root, TryStatementSyntax tryStatement)
    {
        if (tryStatement.Catches.Count > 0)
        {
            return root.ReplaceNode(tryStatement, tryStatement.WithFinally(null));
        }

        // No catch and no cleanup: the try guards nothing, so its body stands on its own.
        if (tryStatement.Parent is not BlockSyntax enclosing)
        {
            return root.ReplaceNode(tryStatement, tryStatement.Block.WithTriviaFrom(tryStatement));
        }

        var index = enclosing.Statements.IndexOf(tryStatement);
        var lifted = enclosing.Statements
            .RemoveAt(index)
            .InsertRange(index, tryStatement.Block.Statements);

        return root.ReplaceNode(enclosing, enclosing.WithStatements(lifted).WithAdditionalAnnotations(Formatter.Annotation));
    }
}
