// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
    /// <summary>The characters the arrow adds between the signature and the expression.</summary>
    private const int ArrowWidth = 4;

    /// <summary>The spaces one indentation level adds to a wrapped continuation line.</summary>
    private const int IndentWidth = 4;

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

        var options = context.Document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(root.SyntaxTree);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryRewrite(root, options, diagnostic) is not { } edit)
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
    {
        var options = editor.OriginalDocument.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(editor.OriginalRoot.SyntaxTree);
        if (TryRewrite(editor.OriginalRoot, options, diagnostic) is not { } edit)
        {
            return;
        }

        editor.ReplaceNode(edit.Original, edit.Replacement);
    }

    /// <summary>Resolves the reported member and rewrites its block body as an expression body.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, AnalyzerConfigOptions options, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent switch
        {
            MethodDeclarationSyntax method when ExpressionBodyAnalyzer.TryGetMethodExpression(method, out var expression)
                => new NodeReplacement(
                    method,
                    Layout(method, options, ToExpressionBody(method, expression))),

            ConstructorDeclarationSyntax constructor when ExpressionBodyAnalyzer.TryGetConstructorExpression(constructor, out var expression)
                => new NodeReplacement(
                    constructor,
                    Layout(constructor, options, ToExpressionBody(constructor, expression))),

            OperatorDeclarationSyntax declared when ExpressionBodyAnalyzer.TryGetOperatorExpression(declared, out var expression)
                => new NodeReplacement(
                    declared,
                    Layout(declared, options, ToExpressionBody(declared, expression))),

            ConversionOperatorDeclarationSyntax conversion when ExpressionBodyAnalyzer.TryGetConversionOperatorExpression(conversion, out var expression)
                => new NodeReplacement(
                    conversion,
                    Layout(conversion, options, ToExpressionBody(conversion, expression))),

            PropertyDeclarationSyntax property when ExpressionBodyAnalyzer.TryGetPropertyExpression(property, out var expression)
                => new NodeReplacement(
                    property,
                    Layout(property, options, ToExpressionBody(property, expression))),

            IndexerDeclarationSyntax indexer when ExpressionBodyAnalyzer.TryGetIndexerExpression(indexer, out var expression)
                => new NodeReplacement(
                    indexer,
                    Layout(indexer, options, ToExpressionBody(indexer, expression))),

            LocalFunctionStatementSyntax localFunction when ExpressionBodyAnalyzer.TryGetLocalFunctionExpression(localFunction, out var expression)
                => new NodeReplacement(
                    localFunction,
                    Layout(localFunction, options, ToExpressionBody(localFunction, expression))),

            _ => null,
        };

    /// <summary>Replaces the block with an expression body while preserving the other children.</summary>
    /// <param name="method">The original member.</param>
    /// <param name="expression">The expression to return or execute.</param>
    /// <returns>The member with an expression body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodDeclarationSyntax ToExpressionBody(MethodDeclarationSyntax method, ExpressionSyntax expression) =>
        method.Update(
            method.AttributeLists,
            method.Modifiers,
            method.ReturnType,
            method.ExplicitInterfaceSpecifier,
            method.Identifier,
            method.TypeParameterList,
            method.ParameterList,
            method.ConstraintClauses,
            null,
            Arrow(expression),
            Semicolon(method.Body!.CloseBraceToken));

    /// <summary>Replaces the block with an expression body while preserving the other children.</summary>
    /// <param name="constructor">The original member.</param>
    /// <param name="expression">The expression to return or execute.</param>
    /// <returns>The member with an expression body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConstructorDeclarationSyntax ToExpressionBody(ConstructorDeclarationSyntax constructor, ExpressionSyntax expression) =>
        constructor.Update(
            constructor.AttributeLists,
            constructor.Modifiers,
            constructor.Identifier,
            constructor.ParameterList,
            constructor.Initializer,
            null,
            Arrow(expression),
            Semicolon(constructor.Body!.CloseBraceToken));

    /// <summary>Replaces the block with an expression body while preserving the other children.</summary>
    /// <param name="declared">The original member.</param>
    /// <param name="expression">The expression to return or execute.</param>
    /// <returns>The member with an expression body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OperatorDeclarationSyntax ToExpressionBody(OperatorDeclarationSyntax declared, ExpressionSyntax expression) =>
        declared.Update(
            declared.AttributeLists,
            declared.Modifiers,
            declared.ReturnType,
            declared.ExplicitInterfaceSpecifier,
            declared.OperatorKeyword,
            declared.CheckedKeyword,
            declared.OperatorToken,
            declared.ParameterList,
            null,
            Arrow(expression),
            Semicolon(declared.Body!.CloseBraceToken));

    /// <summary>Replaces the block with an expression body while preserving the other children.</summary>
    /// <param name="conversion">The original member.</param>
    /// <param name="expression">The expression to return or execute.</param>
    /// <returns>The member with an expression body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConversionOperatorDeclarationSyntax ToExpressionBody(ConversionOperatorDeclarationSyntax conversion, ExpressionSyntax expression) =>
        conversion.Update(
            conversion.AttributeLists,
            conversion.Modifiers,
            conversion.ImplicitOrExplicitKeyword,
            conversion.ExplicitInterfaceSpecifier,
            conversion.OperatorKeyword,
            conversion.CheckedKeyword,
            conversion.Type,
            conversion.ParameterList,
            null,
            Arrow(expression),
            Semicolon(conversion.Body!.CloseBraceToken));

    /// <summary>Replaces the block with an expression body while preserving the other children.</summary>
    /// <param name="property">The original member.</param>
    /// <param name="expression">The expression to return or execute.</param>
    /// <returns>The member with an expression body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyDeclarationSyntax ToExpressionBody(PropertyDeclarationSyntax property, ExpressionSyntax expression) =>
        property.Update(
            property.AttributeLists,
            property.Modifiers,
            property.Type,
            property.ExplicitInterfaceSpecifier,
            property.Identifier,
            null,
            Arrow(expression),
            property.Initializer,
            Semicolon(property.AccessorList!.CloseBraceToken));

    /// <summary>Replaces the block with an expression body while preserving the other children.</summary>
    /// <param name="indexer">The original member.</param>
    /// <param name="expression">The expression to return or execute.</param>
    /// <returns>The member with an expression body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IndexerDeclarationSyntax ToExpressionBody(IndexerDeclarationSyntax indexer, ExpressionSyntax expression) =>
        indexer.Update(
            indexer.AttributeLists,
            indexer.Modifiers,
            indexer.Type,
            indexer.ExplicitInterfaceSpecifier,
            indexer.ThisKeyword,
            indexer.ParameterList,
            null,
            Arrow(expression),
            Semicolon(indexer.AccessorList!.CloseBraceToken));

    /// <summary>Replaces the block with an expression body while preserving the other children.</summary>
    /// <param name="localFunction">The original member.</param>
    /// <param name="expression">The expression to return or execute.</param>
    /// <returns>The member with an expression body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LocalFunctionStatementSyntax ToExpressionBody(LocalFunctionStatementSyntax localFunction, ExpressionSyntax expression) =>
        localFunction.Update(
            localFunction.AttributeLists,
            localFunction.Modifiers,
            localFunction.ReturnType,
            localFunction.Identifier,
            localFunction.TypeParameterList,
            localFunction.ParameterList,
            localFunction.ConstraintClauses,
            null,
            Arrow(expression),
            Semicolon(localFunction.Body!.CloseBraceToken));

    /// <summary>Gets the block body or accessor list a member was written with.</summary>
    /// <param name="original">The member as it was written.</param>
    /// <returns>The body, or <see langword="null"/> when the member has neither.</returns>
    private static SyntaxNode? OriginalBody(SyntaxNode original)
    {
        foreach (var child in original.ChildNodes())
        {
            if (child is BlockSyntax or AccessorListSyntax)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>Gets a member's expression body clause.</summary>
    /// <param name="member">The member to inspect.</param>
    /// <returns>The arrow clause, or <see langword="null"/> when the member has none.</returns>
    private static ArrowExpressionClauseSyntax? ArrowOf(SyntaxNode member)
    {
        foreach (var child in member.ChildNodes())
        {
            if (child is ArrowExpressionClauseSyntax arrow)
            {
                return arrow;
            }
        }

        return null;
    }

    /// <summary>Builds the arrow clause for the collapsed body.</summary>
    /// <param name="expression">The surviving expression.</param>
    /// <returns>An <c>=&gt; expr</c> clause whose arrow keeps a single trailing space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArrowExpressionClauseSyntax Arrow(ExpressionSyntax expression) =>
        SyntaxFactory.ArrowExpressionClause(
            SyntaxFactory.Token(default, SyntaxKind.EqualsGreaterThanToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            expression.WithoutTrivia());

    /// <summary>Builds the closing semicolon, carrying the collapsed body's trailing trivia.</summary>
    /// <param name="closeBrace">The close brace whose trailing trivia the member ends with.</param>
    /// <returns>A semicolon token that keeps the member's trailing trivia.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxToken Semicolon(SyntaxToken closeBrace) =>
        SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.SemicolonToken, closeBrace.TrailingTrivia);

    /// <summary>Lays the new expression body out, wrapping it when one line would run past the maximum.</summary>
    /// <param name="original">The member as it was written.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="rewritten">The member already carrying its expression body.</param>
    /// <returns>The member laid out to fit the line budget.</returns>
    /// <remarks>
    /// A body under a long signature still reads better as an expression body; it just cannot share the
    /// signature's line. The break goes where <c>stylesharp.arrow_token_new_line</c> says, so the wrap does
    /// not simply trade this rule's diagnostic for SST1527's.
    /// </remarks>
    private static SyntaxNode Layout(SyntaxNode original, AnalyzerConfigOptions options, SyntaxNode rewritten)
    {
        var collapsed = Collapse(rewritten);
        if (JoinedLineFits(original, rewritten, options))
        {
            return collapsed;
        }

        var indent = ContinuationIndent(original);
        var newLine = LayoutFixHelpers.DetectNewLine(original.SyntaxTree.GetText());
        var breakBefore = LayoutStyleOptions.ReadBreakBefore(
            options,
            Sst1527ArrowTokenNewLineAnalyzer.SpecificKey,
            Sst1527ArrowTokenNewLineAnalyzer.GeneralKey,
            defaultBreakBefore: false);

        return breakBefore
            ? BreakBeforeArrow(collapsed, newLine, indent)
            : BreakAfterArrow(collapsed, newLine, indent);
    }

    /// <summary>Returns whether the signature and the expression fit on one line together.</summary>
    /// <param name="original">The member as it was written.</param>
    /// <param name="rewritten">The member already carrying its expression body.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <returns><see langword="true"/> when the joined line stays within the maximum.</returns>
    private static bool JoinedLineFits(SyntaxNode original, SyntaxNode rewritten, AnalyzerConfigOptions options)
    {
        if (OriginalBody(original) is not { } body || ArrowOf(rewritten) is not { } arrow)
        {
            return true;
        }

        var expression = arrow.Expression;
        var text = original.SyntaxTree.GetText();
        var signatureEnd = body.GetFirstToken().GetPreviousToken().Span.End;
        var line = text.Lines.GetLineFromPosition(signatureEnd);
        while (signatureEnd > line.Start && char.IsWhiteSpace(text[signatureEnd - 1]))
        {
            signatureEnd--;
        }

        var signatureLength = signatureEnd - line.Start;

        var expressionText = expression.ToString();
        var firstBreak = expressionText.IndexOf('\n');
        var headLength = firstBreak < 0 ? expressionText.Length : firstBreak;
        while (firstBreak >= 0 && headLength > 0 && char.IsWhiteSpace(expressionText[headLength - 1]))
        {
            headLength--;
        }

        return signatureLength + ArrowWidth + headLength + (firstBreak < 0 ? 1 : 0) <= SizeLimitOptions.ReadMaxLineLength(options);
    }

    /// <summary>Gets the indentation a wrapped continuation line uses.</summary>
    /// <param name="original">The member as it was written.</param>
    /// <returns>The member's own indentation plus one level.</returns>
    private static string ContinuationIndent(SyntaxNode original)
    {
        var text = original.SyntaxTree.GetText();
        var line = text.Lines.GetLineFromPosition(original.SpanStart);
        return new(' ', original.SpanStart - line.Start + IndentWidth);
    }

    /// <summary>Puts the arrow at the head of the continuation line.</summary>
    /// <param name="member">The collapsed member.</param>
    /// <param name="newLine">The tree's line ending.</param>
    /// <param name="indent">The continuation indentation.</param>
    /// <returns>The member wrapped before its arrow.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxNode BreakBeforeArrow(SyntaxNode member, string newLine, string indent) =>
        ReplaceArrow(
            member,
            (previous, arrow) => (
                previous.WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine), SyntaxFactory.Whitespace(indent)),
                arrow.WithTrailingTrivia(SyntaxFactory.Space)));

    /// <summary>Leaves the arrow on the signature's line and wraps the expression under it.</summary>
    /// <param name="member">The collapsed member.</param>
    /// <param name="newLine">The tree's line ending.</param>
    /// <param name="indent">The continuation indentation.</param>
    /// <returns>The member wrapped after its arrow.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxNode BreakAfterArrow(SyntaxNode member, string newLine, string indent) =>
        ReplaceArrow(
            member,
            (previous, arrow) => (
                previous.WithTrailingTrivia(SyntaxFactory.Space),
                arrow.WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine), SyntaxFactory.Whitespace(indent))));

    /// <summary>Rewrites the trivia around a member's arrow in one pass.</summary>
    /// <param name="member">The member carrying an expression body.</param>
    /// <param name="layout">Builds the replacement tokens from the token before the arrow and the arrow.</param>
    /// <returns>The member with the arrow laid out, or unchanged when it has no expression body.</returns>
    private static SyntaxNode ReplaceArrow(SyntaxNode member, Func<SyntaxToken, SyntaxToken, (SyntaxToken Previous, SyntaxToken Arrow)> layout)
    {
        foreach (var child in member.ChildNodes())
        {
            if (child is not ArrowExpressionClauseSyntax arrow)
            {
                continue;
            }

            var previous = arrow.ArrowToken.GetPreviousToken();
            var (replacedPrevious, replacedArrow) = layout(previous, arrow.ArrowToken);
            return member.ReplaceTokens(
                [previous, arrow.ArrowToken],
                (original, _) => original == previous ? replacedPrevious : replacedArrow);
        }

        return member;
    }

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
    private static SyntaxTriviaList SingleSpaceKeepingComments(in SyntaxTriviaList trailing)
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
