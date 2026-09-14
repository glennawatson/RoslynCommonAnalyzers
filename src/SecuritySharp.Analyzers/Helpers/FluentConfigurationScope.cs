// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// The local configuration scope around a fluent builder call, shared by the rules that pair two calls on
/// the same builder setup (SES1006, SES1501): the lambda body or single statement that holds the chain, the
/// invoked member name, and the search of that scope for a second builder call. The walk is purely local —
/// no data flow — so calls split across separate statements outside a configuration lambda are never paired.
/// </summary>
internal static class FluentConfigurationScope
{
    /// <summary>Returns the enclosing scope to scan: the nearest lambda body, else the nearest statement or single-expression clause.</summary>
    /// <param name="node">The builder call whose configuration scope is wanted.</param>
    /// <returns>The scope node to search, or <see langword="null"/> when none is found.</returns>
    internal static SyntaxNode? GetScope(SyntaxNode node)
    {
        SyntaxNode? fallbackScope = null;
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            // A configuration lambda is the local unit that holds the whole builder setup: prefer it over any
            // intervening statement so a multi-statement block body is scanned in full.
            if (ancestor is AnonymousFunctionExpressionSyntax lambda)
            {
                return lambda.Body;
            }

            // Outside a lambda the fluent chain lives in one local unit: a statement, an expression-bodied
            // member ('=> chain'), or an initializer ('= chain'). The nearest such unit is the scope.
            fallbackScope ??= ancestor is StatementSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax ? ancestor : null;

            // No lambda encloses the call once a declaration boundary is reached.
            if (ancestor is MemberDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                break;
            }
        }

        return fallbackScope;
    }

    /// <summary>Returns the simple name a member invocation targets, or <see langword="null"/> when there is no receiver.</summary>
    /// <param name="invoked">The invocation's callee expression.</param>
    /// <returns>The invoked member's simple name, or <see langword="null"/> when it is not a member access or member binding.</returns>
    internal static SimpleNameSyntax? GetInvokedName(ExpressionSyntax invoked) =>
        invoked switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => null,
        };

    /// <summary>Returns whether a scope, or any invocation beneath it, is the call the matcher accepts.</summary>
    /// <param name="scope">The scope to search.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The gated builder type passed to the matcher.</param>
    /// <param name="isMatch">Recognises the searched-for builder call.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a matching call is present, the scope node itself included.</returns>
    internal static bool ContainsBuilderCall(
        SyntaxNode scope,
        SemanticModel model,
        INamedTypeSymbol builderType,
        Func<InvocationExpressionSyntax, SemanticModel, INamedTypeSymbol, CancellationToken, bool> isMatch,
        CancellationToken cancellationToken)
    {
        // An expression-lambda body can itself be the outermost matching call of a reversed chain.
        return (scope is InvocationExpressionSyntax rootInvocation && isMatch(rootInvocation, model, builderType, cancellationToken))
            || ContainsMatchingDescendant(scope, model, builderType, isMatch, cancellationToken);
    }

    /// <summary>Walks a node's descendants in document order, testing each invocation.</summary>
    /// <param name="node">The node whose descendants are searched.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The gated builder type passed to the matcher.</param>
    /// <param name="isMatch">Recognises the searched-for builder call.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> at the first descendant invocation the matcher accepts.</returns>
    private static bool ContainsMatchingDescendant(
        SyntaxNode node,
        SemanticModel model,
        INamedTypeSymbol builderType,
        Func<InvocationExpressionSyntax, SemanticModel, INamedTypeSymbol, CancellationToken, bool> isMatch,
        CancellationToken cancellationToken)
    {
        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is not { } child)
            {
                continue;
            }

            if ((child is InvocationExpressionSyntax invocation && isMatch(invocation, model, builderType, cancellationToken))
                || ContainsMatchingDescendant(child, model, builderType, isMatch, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }
}
