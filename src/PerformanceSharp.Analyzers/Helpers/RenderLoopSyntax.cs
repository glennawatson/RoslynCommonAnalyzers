// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Ancestor walks that place a node inside a loop of a render method, shared by the rules that report
/// work repeated on every iteration of a component's render loop (PSH1600, PSH1603). Neither binds anything.
/// </summary>
internal static class RenderLoopSyntax
{
    /// <summary>Returns the nearest enclosing <c>for</c>/<c>foreach</c> reached without crossing a nested function or a member.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns>
    /// The enclosing loop, or <see langword="null"/> when a lambda, anonymous method, or local function is crossed first
    /// (it runs when invoked, not once per iteration) or no loop encloses the node.
    /// </returns>
    internal static SyntaxNode? FindEnclosingLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax:
                    return null;

                case ForStatementSyntax or CommonForEachStatementSyntax:
                    return current;

                case MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Returns the nearest enclosing method declaration, walking through any nested functions.</summary>
    /// <param name="node">The node to search up from.</param>
    /// <returns>The containing method declaration, or <see langword="null"/> when the node is not inside a method.</returns>
    internal static MethodDeclarationSyntax? FindContainingMethod(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax method:
                    return method;

                case BaseTypeDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }
}
