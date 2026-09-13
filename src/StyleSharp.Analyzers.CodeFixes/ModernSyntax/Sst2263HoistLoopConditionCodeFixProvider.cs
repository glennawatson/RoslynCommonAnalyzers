// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Hoists an infinite loop's guard condition into a <c>while</c> header (SST2263): the guarded work becomes the
/// loop body and the condition heads the loop. The rewritten loop is formatter-annotated so the lifted body is
/// re-indented to its new depth.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2263HoistLoopConditionCodeFixProvider))]
[Shared]
public sealed class Sst2263HoistLoopConditionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.HoistLoopCondition.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Hoist the condition into the loop header", nameof(Sst2263HoistLoopConditionCodeFixProvider), CanRewrite, TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        for (var current = root.FindNode(diagnostic.Location.SourceSpan); current is not null; current = current.Parent)
        {
            if (current is not (WhileStatementSyntax or ForStatementSyntax))
            {
                continue;
            }

            return CanHoist((StatementSyntax)current);
        }

        return false;
    }

    /// <summary>Checks the infinite loop and its leading break without constructing the new condition.</summary>
    /// <param name="loop">The nearest loop.</param>
    /// <returns>Whether the leading condition can be hoisted.</returns>
    private static bool CanHoist(StatementSyntax loop) =>
        loop switch
        {
            WhileStatementSyntax { Statement: BlockSyntax body } whileLoop when whileLoop.Condition.IsKind(SyntaxKind.TrueLiteralExpression) => CanHoistBody(body),
            ForStatementSyntax { Condition: null, Declaration: null, Initializers.Count: 0, Incrementors.Count: 0, Statement: BlockSyntax body } => CanHoistBody(body),
            _ => false,
        };

    /// <summary>Checks the leading break shape without removing statements or negating syntax.</summary>
    /// <param name="block">The infinite loop body.</param>
    /// <returns>Whether the body starts with a hoistable break condition.</returns>
    private static bool CanHoistBody(BlockSyntax block) =>
        block.Statements.Count > 0
            && block.Statements[0] is IfStatementSyntax guard
            && (block.Statements.Count == 1
                ? guard.Else is { } otherwise
                    && otherwise.Statement is BreakStatementSyntax or BlockSyntax { Statements: [BreakStatementSyntax] }
                    && guard.Statement is not (EmptyStatementSyntax or BlockSyntax { Statements.Count: 0 })
                : guard.Else is null
                    && guard.Statement is BreakStatementSyntax or BlockSyntax { Statements: [BreakStatementSyntax] });

    /// <summary>Resolves the reported loop and rewrites it with the hoisted condition.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is not (WhileStatementSyntax or ForStatementSyntax))
            {
                continue;
            }

            var loop = (StatementSyntax)current;
            return Sst2263HoistLoopConditionAnalyzer.TryGetHoist(loop, out var condition, out var body)
                ? new NodeReplacement(loop, BuildWhile(loop, condition, body))
                : null;
        }

        return null;
    }

    /// <summary>Builds the hoisted <c>while</c> loop.</summary>
    /// <param name="loop">The original loop, used for its outer trivia.</param>
    /// <param name="condition">The condition to head the loop.</param>
    /// <param name="body">The loop body.</param>
    /// <returns>The formatter-annotated <c>while</c> loop.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static WhileStatementSyntax BuildWhile(StatementSyntax loop, ExpressionSyntax condition, StatementSyntax body) =>
        SyntaxFactory.WhileStatement(
                default,
                SyntaxFactory.Token(loop.GetLeadingTrivia(), SyntaxKind.WhileKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                condition.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.CloseParenToken),
                body.WithTrailingTrivia(loop.GetTrailingTrivia()))
            .WithAdditionalAnnotations(Formatter.Annotation);
}
