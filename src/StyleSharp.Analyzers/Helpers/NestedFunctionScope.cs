// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads whether code runs in a nested function: a lambda, an anonymous method or a local function. A local
/// referenced from one is captured, can outlive the enclosing method, and runs whenever the function is called.
/// </summary>
internal static class NestedFunctionScope
{
    /// <summary>Returns whether a lambda, anonymous method or local function encloses a node below a boundary.</summary>
    /// <param name="node">The node to place.</param>
    /// <param name="boundary">The node that bounds the walk; functions at or above it do not count.</param>
    /// <returns><see langword="true"/> when a nested function sits between the node and the boundary.</returns>
    internal static bool IsInsideNestedFunction(SyntaxNode node, SyntaxNode boundary)
    {
        for (var current = node.Parent; current is not null && current != boundary; current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }
}
