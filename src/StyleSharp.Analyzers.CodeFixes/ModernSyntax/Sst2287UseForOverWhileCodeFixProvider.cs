// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(ModernSyntaxRules.UseForOverWhile.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (!context.Document.TryGetSyntaxRoot(out var root))
        {
            root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        }

        if (root is null)
        {
            return;
        }

        SemanticModel? model = null;
        foreach (var diagnostic in context.Diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var span = diagnostic.Location.SourceSpan;
            if (!root.FullSpan.Contains(span)
                || root.FindNode(span).FirstAncestorOrSelf<WhileStatementSyntax>() is not { } loop)
            {
                continue;
            }

            if (model is null
                && !context.Document.TryGetSemanticModel(out model))
            {
                model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            }

            if (model is null
                || !WhileLoopCounter.TryMatch(loop, model, context.CancellationToken, out var parts))
            {
                continue;
            }

            context.RegisterCodeFix(
                CreateCodeAction(context.Document, root, loop, parts),
                diagnostic);
        }
    }

    /// <summary>Creates the lazy action after the loop has passed all eligibility checks.</summary>
    /// <param name="document">The document containing the loop.</param>
    /// <param name="root">The cached syntax root.</param>
    /// <param name="loop">The eligible while loop.</param>
    /// <param name="parts">The counter parts gathered for the loop.</param>
    /// <returns>The code action that rewrites the loop when applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CodeAction CreateCodeAction(
        Document document,
        SyntaxNode root,
        WhileStatementSyntax loop,
        WhileLoopCounterParts parts) =>
        CodeAction.Create(
            "Convert to a for loop",
            _ => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(loop.Parent!, Rewrite(loop, parts)))),
            nameof(Sst2287UseForOverWhileCodeFixProvider));

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
        var spaced = SyntaxFactory.Token(default, SyntaxKind.SemicolonToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));

        return SyntaxFactory.ForStatement(
            attributeLists: default,
            forKeyword: SyntaxFactory.Token(parts.Declaration.GetLeadingTrivia(), SyntaxKind.ForKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            openParenToken: SyntaxFactory.Token(SyntaxKind.OpenParenToken),
            declaration: parts.Declaration.Declaration.WithoutTrivia(),
            initializers: default,
            firstSemicolonToken: spaced,
            condition: loop.Condition.WithoutTrivia(),
            secondSemicolonToken: spaced,
            incrementors: SyntaxFactory.SingletonSeparatedList(parts.Incrementor.Expression.WithoutTrivia()),
            closeParenToken: SyntaxFactory.Token(SyntaxKind.CloseParenToken),
            statement: body.WithStatements(remaining));
    }
}
