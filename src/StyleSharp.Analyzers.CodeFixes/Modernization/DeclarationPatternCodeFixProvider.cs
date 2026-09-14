// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an <c>is</c> check followed by a cast local as a declaration pattern (SST2007).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DeclarationPatternCodeFixProvider))]
[Shared]
public sealed class DeclarationPatternCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseDeclarationPatternOverIsCheckAndCast.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Declare the checked value in the pattern",
            nameof(DeclarationPatternCodeFixProvider),
            static (root, diagnostic) => Resolve(root, diagnostic) is not null,
            TryRewrite);

    /// <summary>Resolves the reported <c>if</c> and builds it with the cast local folded into a declaration pattern.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The if statement and its replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (Resolve(root, diagnostic) is not { } ifStatement)
        {
            return null;
        }

        // Resolve has matched every shape cast below.
        var isExpression = (BinaryExpressionSyntax)ExpressionShapes.WalkDownParentheses(ifStatement.Condition);
        var block = (BlockSyntax)ifStatement.Statement;
        var local = block.Statements[0];
        var identifier = ((LocalDeclarationStatementSyntax)local).Declaration.Variables[0].Identifier;
        var pattern = SyntaxFactory.DeclarationPattern(
            ((TypeSyntax)isExpression.Right).WithoutTrivia(),
            SyntaxFactory.SingleVariableDesignation(
                SyntaxFactory.Identifier(
                    SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker),
                    identifier.ValueText,
                    ifStatement.Condition.GetTrailingTrivia())));
        var condition = SyntaxFactory.IsPatternExpression(
            isExpression.Left.WithoutTrailingTrivia().WithLeadingTrivia(ifStatement.Condition.GetLeadingTrivia()),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.IsKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
            pattern);
        return block.RemoveNode(local, SyntaxRemoveOptions.KeepNoTrivia) is { } replacementBlock
            ? new NodeReplacement(
                ifStatement,
                ifStatement.Update(
                    ifStatement.AttributeLists,
                    ifStatement.IfKeyword,
                    ifStatement.OpenParenToken,
                    condition,
                    ifStatement.CloseParenToken,
                    replacementBlock,
                    ifStatement.Else))
            : null;
    }

    /// <summary>Resolves the reported <c>if</c> when it tests a type and its block opens with the single local cast from it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The if statement, or <see langword="null"/> when the shape no longer matches or a directive crosses it.</returns>
    private static IfStatementSyntax? Resolve(SyntaxNode root, Diagnostic diagnostic) =>
        DiagnosticAncestor.Find<IfStatementSyntax>(root, diagnostic.Location.SourceSpan) is { } ifStatement
            && ExpressionShapes.WalkDownParentheses(ifStatement.Condition) is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax }
            && ifStatement.Statement is BlockSyntax { Statements.Count: > 0 } block
            && block.Statements[0] is LocalDeclarationStatementSyntax { Declaration.Variables: { Count: 1 } variables }
            && variables[0] is { Initializer.Value: CastExpressionSyntax }
            && !DirectiveBoundaries.Cross(ifStatement, ifStatement.Span)
            ? ifStatement
            : null;
}
