// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites the method or local function reported by SST2491 to be <c>async</c> and to <c>await</c> its
/// returned tasks, so the teardown runs after the work completes. A generic task return becomes
/// <c>return await X;</c>; a non-generic <c>Task</c>/<c>ValueTask</c> return becomes <c>await X; return;</c>.
/// The fix is withheld where <c>await</c> would not compile — a return inside a <c>lock</c> body — leaving
/// that restructuring to the author.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2491AwaitableReturnedFromTeardownCodeFixProvider))]
[Shared]
public sealed class Sst2491AwaitableReturnedFromTeardownCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix, IBatchEditKeyProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(CorrectnessRules.AwaitableReturnedFromTeardown.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Make the method 'async' and await the call",
            nameof(Sst2491AwaitableReturnedFromTeardownCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <inheritdoc/>
    bool IBatchEditKeyProvider.TryGetBatchEditSpan(SyntaxNode root, Diagnostic diagnostic, out TextSpan span)
    {
        var function = FindFixableFunction(root, diagnostic);
        span = function?.Span ?? default;
        return function is not null;
    }

    /// <summary>Resolves the reported return to its function and builds the async replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no fix compiles here.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (FindFixableFunction(root, diagnostic) is not { } function
            || Decompose(function) is not (var modifiers, { } body)
            || modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return null;
        }

        var returns = new List<ReturnStatementSyntax>();
        CollectOwnedReturns(body, returns);
        if (returns.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < returns.Count; i++)
        {
            if (IsInsideLock(returns[i], function))
            {
                return null;
            }
        }

        if (model.GetDeclaredSymbol(function) is not IMethodSymbol method
            || method.ReturnType is not INamedTypeSymbol returnType)
        {
            return null;
        }

        var producesValue = returnType.IsGenericType;
        var newBody = body.ReplaceNodes(returns, (original, _) => Awaitify(original, producesValue));
        var rewritten = ToAsyncFunction(function, newBody).WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
        return new NodeReplacement(function, rewritten);
    }

    /// <summary>Finds the method or local function owning the reported return, when it is fixable.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The owning method or local function, or <see langword="null"/> when it is neither.</returns>
    private static SyntaxNode? FindFixableFunction(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<ReturnStatementSyntax>() is not { } returnStatement)
        {
            return null;
        }

        for (var node = returnStatement.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case MethodDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                    return node;
                case AnonymousFunctionExpressionSyntax:
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Reads the modifiers and block body of a method or local function.</summary>
    /// <param name="function">The function node.</param>
    /// <returns>The modifiers and block body; the body is <see langword="null"/> when the function is expression-bodied.</returns>
    private static (SyntaxTokenList Modifiers, BlockSyntax? Body) Decompose(SyntaxNode function) => function switch
    {
        MethodDeclarationSyntax method => (method.Modifiers, method.Body),
        LocalFunctionStatementSyntax localFunction => (localFunction.Modifiers, localFunction.Body),
        _ => (default, null),
    };

    /// <summary>Collects the returns owned by a function's body, not descending into nested functions.</summary>
    /// <param name="node">The node to scan.</param>
    /// <param name="returns">The accumulating list of owned returns.</param>
    private static void CollectOwnedReturns(SyntaxNode node, List<ReturnStatementSyntax> returns)
    {
        foreach (var child in node.ChildNodes())
        {
            if (child is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            {
                continue;
            }

            if (child is ReturnStatementSyntax { Expression: not null } returnStatement)
            {
                returns.Add(returnStatement);
                continue;
            }

            CollectOwnedReturns(child, returns);
        }
    }

    /// <summary>Returns whether a return sits inside a <c>lock</c> body within its function.</summary>
    /// <param name="returnStatement">The return statement.</param>
    /// <param name="function">The owning function.</param>
    /// <returns><see langword="true"/> when awaiting the return would not compile.</returns>
    private static bool IsInsideLock(ReturnStatementSyntax returnStatement, SyntaxNode function)
    {
        for (var node = returnStatement.Parent; node is not null && node != function; node = node.Parent)
        {
            if (node is LockStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Rewrites one return so its task is awaited.</summary>
    /// <param name="returnStatement">The return to rewrite.</param>
    /// <param name="producesValue">Whether the method returns a generic task and the awaited value is returned.</param>
    /// <returns>The awaited replacement statement.</returns>
    private static StatementSyntax Awaitify(ReturnStatementSyntax returnStatement, bool producesValue)
    {
        var expression = returnStatement.Expression!;
        var awaitKeyword = SyntaxFactory.Token(SyntaxKind.AwaitKeyword)
            .WithLeadingTrivia(expression.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.Space);
        var awaited = SyntaxFactory.AwaitExpression(awaitKeyword, expression.WithLeadingTrivia());

        if (producesValue)
        {
            return returnStatement.WithExpression(awaited);
        }

        var awaitStatement = SyntaxFactory.ExpressionStatement(awaited);
        var bareReturn = SyntaxFactory.ReturnStatement();
        return SyntaxFactory.Block(awaitStatement, bareReturn).WithLeadingTrivia(returnStatement.GetLeadingTrivia());
    }

    /// <summary>Rebuilds a function node with the <c>async</c> modifier and a rewritten body.</summary>
    /// <param name="function">The original function.</param>
    /// <param name="body">The rewritten body.</param>
    /// <returns>The async function.</returns>
    private static SyntaxNode ToAsyncFunction(SyntaxNode function, BlockSyntax body)
    {
        if (function is MethodDeclarationSyntax method)
        {
            var (modifiers, returnType) = WithAsyncModifier(method.Modifiers, method.ReturnType);
            return method.WithModifiers(modifiers).WithReturnType(returnType).WithBody(body);
        }

        var localFunction = (LocalFunctionStatementSyntax)function;
        var (localModifiers, localReturnType) = WithAsyncModifier(localFunction.Modifiers, localFunction.ReturnType);
        return localFunction.WithModifiers(localModifiers).WithReturnType(localReturnType).WithBody(body);
    }

    /// <summary>Adds the <c>async</c> modifier immediately before the return type, carrying trivia across.</summary>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <param name="returnType">The declaration's return type.</param>
    /// <returns>The modifiers with async, and the return type adjusted when it led the declaration.</returns>
    private static (SyntaxTokenList Modifiers, TypeSyntax ReturnType) WithAsyncModifier(SyntaxTokenList modifiers, TypeSyntax returnType)
    {
        var asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword);
        if (modifiers.Count == 0)
        {
            var leading = asyncToken.WithLeadingTrivia(returnType.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.Space);
            return (SyntaxFactory.TokenList(leading), returnType.WithLeadingTrivia());
        }

        var last = modifiers[modifiers.Count - 1];
        var placed = asyncToken.WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(last.TrailingTrivia);
        return (modifiers.Replace(last, last.WithTrailingTrivia()).Add(placed), returnType);
    }
}
