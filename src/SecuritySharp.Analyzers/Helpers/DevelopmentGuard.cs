// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Recognises the <c>IsDevelopment</c> guard that makes an otherwise unsafe call acceptable. Several
/// rules report a call that must not reach production — a developer exception page, sensitive framework
/// diagnostics, plain-HTTP metadata — and each of them has to stay silent when the call is already
/// fenced off to the development environment.
/// </summary>
/// <remarks>
/// The check is syntactic on purpose. The environment abstraction the guard is called through varies by
/// framework version and by how the host is wired up, so binding the symbol would make the rule depend
/// on a reference the analyzed project may not carry. A method named <c>IsDevelopment</c> enclosing the
/// call is evidence enough to stay quiet, and staying quiet on a near-miss is the safe direction.
/// </remarks>
internal static class DevelopmentGuard
{
    /// <summary>The method name a development-environment guard is written with.</summary>
    private const string MethodName = "IsDevelopment";

    /// <summary>Returns whether an enclosing <c>if</c> or conditional guards a node with an <c>IsDevelopment</c> check.</summary>
    /// <param name="node">The reported node.</param>
    /// <returns><see langword="true"/> when a development-environment guard lexically encloses the node.</returns>
    internal static bool Encloses(SyntaxNode node)
    {
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            var condition = ancestor switch
            {
                IfStatementSyntax ifStatement => ifStatement.Condition,
                ConditionalExpressionSyntax conditional => conditional.Condition,
                _ => null,
            };

            if (condition is not null && ContainsGuardCall(condition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a condition subtree calls a method named <c>IsDevelopment</c>.</summary>
    /// <param name="condition">The guard condition to scan.</param>
    /// <returns><see langword="true"/> when the condition contains an <c>IsDevelopment</c> invocation.</returns>
    private static bool ContainsGuardCall(ExpressionSyntax condition)
    {
        if (IsGuardInvocation(condition))
        {
            return true;
        }

        var found = false;
        _ = DescendantTraversalHelper.VisitDescendants(
            condition,
            ref found,
            static (InvocationExpressionSyntax invocation, ref bool state) =>
            {
                if (!IsGuardInvocation(invocation))
                {
                    return true;
                }

                state = true;
                return false;
            });

        return found;
    }

    /// <summary>Returns whether a node is an invocation of a method named <c>IsDevelopment</c>.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns><see langword="true"/> for an <c>IsDevelopment</c> invocation.</returns>
    private static bool IsGuardInvocation(SyntaxNode node) =>
        node is InvocationExpressionSyntax invocation && InvokedName.Of(invocation.Expression) is MethodName;
}
