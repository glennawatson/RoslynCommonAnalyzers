// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes the pass-through async state machine reported by PSH1311: drops the <c>async</c>
/// modifier and returns the awaited task directly — <c>=> await X</c> becomes <c>=> X</c>,
/// <c>return await X;</c> becomes <c>return X;</c>, and a lone <c>await X;</c> becomes
/// <c>return X;</c>. A trailing <c>.ConfigureAwait(...)</c> is stripped; with nothing after
/// the await, the context switch it controls no longer exists.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1311RemovePassThroughStateMachineCodeFixProvider))]
[Shared]
public sealed class Psh1311RemovePassThroughStateMachineCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.RemovePassThroughStateMachine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Return the task directly", nameof(Psh1311RemovePassThroughStateMachineCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Applies the pass-through rewrite to one method declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="method">The async pass-through method to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, MethodDeclarationSyntax method)
        => Psh1311RemovePassThroughStateMachineAnalyzer.TryGetShape(method, out _, out _, out _)
            ? document.WithSyntaxRoot(root.ReplaceNode(method, Rewrite(method)))
            : document;

    /// <summary>Resolves the reported declaration and builds its de-async'd replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (!Psh1311RemovePassThroughStateMachineAnalyzer.TryGetShape(node, out _, out _, out _))
        {
            return null;
        }

        return node switch
        {
            MethodDeclarationSyntax method => new NodeReplacement(method, Rewrite(method)),
            LocalFunctionStatementSyntax localFunction => new NodeReplacement(localFunction, Rewrite(localFunction)),
            _ => null,
        };
    }

    /// <summary>Builds the non-async replacement for a pass-through method declaration.</summary>
    /// <param name="method">The method to rewrite.</param>
    /// <returns>The rewritten method, annotated for formatting.</returns>
    private static MethodDeclarationSyntax Rewrite(MethodDeclarationSyntax method)
    {
        var returnType = method.ReturnType;
        var modifiers = RemoveAsyncModifier(method.Modifiers, ref returnType);
        var updated = method.WithModifiers(modifiers).WithReturnType(returnType);
        updated = updated.ExpressionBody is { } arrow
            ? updated.WithExpressionBody(RewriteArrow(arrow))
            : updated.WithBody(RewriteBlock(updated.Body!));
        return updated.WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Builds the non-async replacement for a pass-through local function.</summary>
    /// <param name="localFunction">The local function to rewrite.</param>
    /// <returns>The rewritten local function, annotated for formatting.</returns>
    private static LocalFunctionStatementSyntax Rewrite(LocalFunctionStatementSyntax localFunction)
    {
        var returnType = localFunction.ReturnType;
        var modifiers = RemoveAsyncModifier(localFunction.Modifiers, ref returnType);
        var updated = localFunction.WithModifiers(modifiers).WithReturnType(returnType);
        updated = updated.ExpressionBody is { } arrow
            ? updated.WithExpressionBody(RewriteArrow(arrow))
            : updated.WithBody(RewriteBlock(updated.Body!));
        return updated.WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Removes the <c>async</c> modifier, carrying its leading trivia onto whatever token now leads the declaration.</summary>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <param name="returnType">The declaration's return type, updated when the async keyword was the only modifier.</param>
    /// <returns>The modifiers without the async keyword.</returns>
    private static SyntaxTokenList RemoveAsyncModifier(SyntaxTokenList modifiers, ref TypeSyntax returnType)
    {
        var index = modifiers.IndexOf(SyntaxKind.AsyncKeyword);
        var asyncToken = modifiers[index];
        var updated = modifiers.RemoveAt(index);
        if (index != 0 || asyncToken.LeadingTrivia.Count == 0)
        {
            return updated;
        }

        if (updated.Count > 0)
        {
            return updated.Replace(updated[0], updated[0].WithLeadingTrivia(asyncToken.LeadingTrivia.AddRange(updated[0].LeadingTrivia)));
        }

        returnType = returnType.WithLeadingTrivia(asyncToken.LeadingTrivia.AddRange(returnType.GetLeadingTrivia()));
        return updated;
    }

    /// <summary>Unwraps an arrow body's <c>await X</c> to the bare forwarded task.</summary>
    /// <param name="arrow">The expression body whose expression is the reported await.</param>
    /// <returns>The arrow clause forwarding the task directly.</returns>
    private static ArrowExpressionClauseSyntax RewriteArrow(ArrowExpressionClauseSyntax arrow)
        => arrow.WithExpression(UnwrapForwardedTask((AwaitExpressionSyntax)arrow.Expression));

    /// <summary>Rewrites a single-statement body's <c>return await X;</c> or <c>await X;</c> to <c>return X;</c>.</summary>
    /// <param name="body">The block whose one statement is the reported await.</param>
    /// <returns>The block returning the task directly.</returns>
    private static BlockSyntax RewriteBlock(BlockSyntax body)
    {
        if (body.Statements[0] is ReturnStatementSyntax { Expression: AwaitExpressionSyntax returned } returnStatement)
        {
            return body.ReplaceNode(returnStatement, returnStatement.WithExpression(UnwrapForwardedTask(returned)));
        }

        var statement = (ExpressionStatementSyntax)body.Statements[0];
        var awaited = (AwaitExpressionSyntax)statement.Expression;
        var replacement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                UnwrapForwardedTask(awaited).WithLeadingTrivia(),
                statement.SemicolonToken)
            .WithLeadingTrivia(statement.GetLeadingTrivia());
        return body.ReplaceNode(statement, replacement);
    }

    /// <summary>Builds the forwarded task expression for a reported await, stripping a trailing ConfigureAwait.</summary>
    /// <param name="awaitExpression">The reported await expression.</param>
    /// <returns>The bare task expression carrying the await expression's trivia.</returns>
    private static ExpressionSyntax UnwrapForwardedTask(AwaitExpressionSyntax awaitExpression)
        => Psh1311RemovePassThroughStateMachineAnalyzer.UnwrapConfigureAwait(awaitExpression.Expression).WithTriviaFrom(awaitExpression);
}
