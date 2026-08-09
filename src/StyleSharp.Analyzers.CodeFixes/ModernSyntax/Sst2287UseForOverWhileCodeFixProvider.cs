// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.CodeActions;

namespace StyleSharp.Analyzers;

/// <summary>
/// Gathers a while loop's counter declaration, condition, and trailing step into a <c>for</c> header
/// (SST2287). The declaration above the loop and the step at the end of the body both move into the header,
/// so the loop keeps its meaning and the counter stops outliving it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2287UseForOverWhileCodeFixProvider))]
[Shared]
public sealed class Sst2287UseForOverWhileCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(ModernSyntaxRules.UseForOverWhile.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<WhileStatementSyntax>() is not { } loop
                || !WhileLoopCounter.TryMatch(loop, model, context.CancellationToken, out var parts))
            {
                continue;
            }

            var rewritten = root.ReplaceNode(loop.Parent!, Rewrite(loop, parts));
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Convert to a for loop",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(rewritten)),
                    nameof(Sst2287UseForOverWhileCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Builds the enclosing block with the declaration and the loop replaced by one for statement.</summary>
    /// <param name="loop">The while statement.</param>
    /// <param name="parts">The counter parts the header gathers.</param>
    /// <returns>The rewritten enclosing block.</returns>
    private static BlockSyntax Rewrite(WhileStatementSyntax loop, WhileLoopCounterParts parts)
    {
        var enclosing = (BlockSyntax)loop.Parent!;
        var forStatement = BuildForStatement(loop, parts);

        var statements = enclosing.Statements;
        var rewritten = new List<StatementSyntax>(statements.Count - 1);
        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            if (ReferenceEquals(statement, loop))
            {
                continue;
            }

            rewritten.Add(ReferenceEquals(statement, parts.Declaration) ? forStatement : statement);
        }

        return enclosing.WithStatements(SyntaxFactory.List(rewritten));
    }

    /// <summary>Builds the for statement that replaces the declaration and the loop.</summary>
    /// <param name="loop">The while statement.</param>
    /// <param name="parts">The counter parts the header gathers.</param>
    /// <returns>The for statement, spaced and indented like the declaration it replaces.</returns>
    private static ForStatementSyntax BuildForStatement(WhileStatementSyntax loop, WhileLoopCounterParts parts)
    {
        var body = (BlockSyntax)loop.Statement;
        var remaining = body.Statements.RemoveAt(body.Statements.Count - 1);
        var spaced = SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(SyntaxFactory.Space);

        return SyntaxFactory.ForStatement(
                declaration: parts.Declaration.Declaration.WithoutTrivia(),
                initializers: default,
                condition: loop.Condition.WithoutTrivia(),
                incrementors: SyntaxFactory.SingletonSeparatedList(parts.Incrementor.Expression.WithoutTrivia()),
                statement: body.WithStatements(remaining))
            .WithForKeyword(SyntaxFactory.Token(SyntaxKind.ForKeyword).WithTrailingTrivia(SyntaxFactory.Space))
            .WithFirstSemicolonToken(spaced)
            .WithSecondSemicolonToken(spaced)
            .WithLeadingTrivia(parts.Declaration.GetLeadingTrivia())
            .WithTrailingTrivia(loop.GetTrailingTrivia());
    }
}
