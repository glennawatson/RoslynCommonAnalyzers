// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Hoist the condition into the loop header", nameof(Sst2263HoistLoopConditionCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported loop and rewrites it with the hoisted condition.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is WhileStatementSyntax or ForStatementSyntax)
            {
                var loop = (StatementSyntax)current;
                return Sst2263HoistLoopConditionAnalyzer.TryGetHoist(loop, out var condition, out var body)
                    ? new NodeReplacement(loop, BuildWhile(loop, condition, body))
                    : null;
            }
        }

        return null;
    }

    /// <summary>Builds the hoisted <c>while</c> loop.</summary>
    /// <param name="loop">The original loop, used for its outer trivia.</param>
    /// <param name="condition">The condition to head the loop.</param>
    /// <param name="body">The loop body.</param>
    /// <returns>The formatter-annotated <c>while</c> loop.</returns>
    private static WhileStatementSyntax BuildWhile(StatementSyntax loop, ExpressionSyntax condition, StatementSyntax body)
        => SyntaxFactory.WhileStatement(condition.WithoutTrivia(), body)
            .WithLeadingTrivia(loop.GetLeadingTrivia())
            .WithTrailingTrivia(loop.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
}
