// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a member's single-statement block body as an expression body <c>=&gt; expr</c> for every kind the
/// analyzer reports: a method (SST2275), a constructor (SST2276), an operator (SST2277), a conversion operator
/// (SST2278), a get-only property (SST2279), a get-only indexer (SST2280), and a local function (SST2281). The
/// surviving expression and the member's trailing trivia carry through, and the arrow keeps a single trailing space.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExpressionBodyCodeFixProvider))]
[Shared]
public sealed class ExpressionBodyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.UseExpressionBodyForMethod.Id,
        ModernSyntaxRules.UseExpressionBodyForConstructor.Id,
        ModernSyntaxRules.UseExpressionBodyForOperator.Id,
        ModernSyntaxRules.UseExpressionBodyForConversionOperator.Id,
        ModernSyntaxRules.UseExpressionBodyForProperty.Id,
        ModernSyntaxRules.UseExpressionBodyForIndexer.Id,
        ModernSyntaxRules.UseExpressionBodyForLocalFunction.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (TryRewrite(root, diagnostic) is not { } edit)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use an expression body",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported member and rewrites its block body as an expression body.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent switch
        {
            MethodDeclarationSyntax method when ExpressionBodyAnalyzer.TryGetMethodExpression(method, out var expression)
                => new NodeReplacement(
                    method,
                    Collapse(method.WithBody(null).WithExpressionBody(Arrow(expression)).WithSemicolonToken(Semicolon(method.Body!.CloseBraceToken)))),

            ConstructorDeclarationSyntax constructor when ExpressionBodyAnalyzer.TryGetConstructorExpression(constructor, out var expression)
                => new NodeReplacement(
                    constructor,
                    Collapse(constructor.WithBody(null).WithExpressionBody(Arrow(expression)).WithSemicolonToken(Semicolon(constructor.Body!.CloseBraceToken)))),

            OperatorDeclarationSyntax operatorDeclaration when ExpressionBodyAnalyzer.TryGetOperatorExpression(operatorDeclaration, out var expression)
                => new NodeReplacement(
                    operatorDeclaration,
                    Collapse(operatorDeclaration.WithBody(null).WithExpressionBody(Arrow(expression)).WithSemicolonToken(Semicolon(operatorDeclaration.Body!.CloseBraceToken)))),

            ConversionOperatorDeclarationSyntax conversion when ExpressionBodyAnalyzer.TryGetConversionOperatorExpression(conversion, out var expression)
                => new NodeReplacement(
                    conversion,
                    Collapse(conversion.WithBody(null).WithExpressionBody(Arrow(expression)).WithSemicolonToken(Semicolon(conversion.Body!.CloseBraceToken)))),

            PropertyDeclarationSyntax property when ExpressionBodyAnalyzer.TryGetPropertyExpression(property, out var expression)
                => new NodeReplacement(
                    property,
                    Collapse(property.WithAccessorList(null).WithExpressionBody(Arrow(expression)).WithSemicolonToken(Semicolon(property.AccessorList!.CloseBraceToken)))),

            IndexerDeclarationSyntax indexer when ExpressionBodyAnalyzer.TryGetIndexerExpression(indexer, out var expression)
                => new NodeReplacement(
                    indexer,
                    Collapse(indexer.WithAccessorList(null).WithExpressionBody(Arrow(expression)).WithSemicolonToken(Semicolon(indexer.AccessorList!.CloseBraceToken)))),

            LocalFunctionStatementSyntax localFunction when ExpressionBodyAnalyzer.TryGetLocalFunctionExpression(localFunction, out var expression)
                => new NodeReplacement(
                    localFunction,
                    Collapse(localFunction.WithBody(null).WithExpressionBody(Arrow(expression)).WithSemicolonToken(Semicolon(localFunction.Body!.CloseBraceToken)))),

            _ => null,
        };

    /// <summary>Builds the arrow clause for the collapsed body.</summary>
    /// <param name="expression">The surviving expression.</param>
    /// <returns>An <c>=&gt; expr</c> clause whose arrow keeps a single trailing space.</returns>
    private static ArrowExpressionClauseSyntax Arrow(ExpressionSyntax expression)
        => SyntaxFactory.ArrowExpressionClause(
            SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTrailingTrivia(SyntaxFactory.Space),
            expression.WithoutTrivia());

    /// <summary>Builds the closing semicolon, carrying the collapsed body's trailing trivia.</summary>
    /// <param name="closeBrace">The close brace whose trailing trivia the member ends with.</param>
    /// <returns>A semicolon token that keeps the member's trailing trivia.</returns>
    private static SyntaxToken Semicolon(SyntaxToken closeBrace)
        => SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(closeBrace.TrailingTrivia);

    /// <summary>Pulls the arrow up beside the signature by collapsing the whitespace that preceded the old brace.</summary>
    /// <param name="member">The member already rewritten to an expression body.</param>
    /// <returns>The member with a single space before its arrow, keeping any comment that was there.</returns>
    private static SyntaxNode Collapse(SyntaxNode member)
    {
        foreach (var child in member.ChildNodes())
        {
            if (child is not ArrowExpressionClauseSyntax arrow)
            {
                continue;
            }

            var previous = arrow.ArrowToken.GetPreviousToken();
            return member.ReplaceToken(previous, previous.WithTrailingTrivia(SingleSpaceKeepingComments(previous.TrailingTrivia)));
        }

        return member;
    }

    /// <summary>Reduces a token's trailing trivia to a single space, keeping any comment it carried.</summary>
    /// <param name="trailing">The trailing trivia that used to sit before the block's open brace.</param>
    /// <returns>The trivia with newlines and stray whitespace collapsed to one trailing space.</returns>
    private static SyntaxTriviaList SingleSpaceKeepingComments(SyntaxTriviaList trailing)
    {
        var kept = new List<SyntaxTrivia>(trailing.Count + 1);
        foreach (var trivia in trailing)
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                continue;
            }

            if (kept.Count == 0)
            {
                kept.Add(SyntaxFactory.Space);
            }

            kept.Add(trivia);
        }

        kept.Add(SyntaxFactory.Space);
        return SyntaxFactory.TriviaList(kept);
    }
}
