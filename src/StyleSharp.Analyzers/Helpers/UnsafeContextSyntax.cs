// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Recognizes the syntax forms that only compile inside an unsafe context: pointer and function-pointer types,
/// <c>fixed</c>, <c>sizeof</c>, dereference, pointer member access, address-of and a nested <c>unsafe</c> block.
/// A declaration marked <c>unsafe</c> that contains none of them gains nothing from the modifier.
/// </summary>
internal static class UnsafeContextSyntax
{
    /// <summary>Returns whether any node beneath a declaration requires an unsafe context.</summary>
    /// <param name="node">The declaration whose descendants are inspected; the node itself is not.</param>
    /// <returns><see langword="true"/> when the declaration really does need an unsafe context.</returns>
    /// <remarks>The walk stops at the first unsafe-only form.</remarks>
    internal static bool Contains(SyntaxNode node)
    {
        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is { } child && (RequiresUnsafeContext(child) || Contains(child)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node is one of the syntax forms only an unsafe context allows.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns><see langword="true"/> for pointer, fixed, sizeof, address-of and unsafe-statement forms.</returns>
    internal static bool RequiresUnsafeContext(SyntaxNode node) =>
        node.Kind() is SyntaxKind.PointerType
            or SyntaxKind.FunctionPointerType
            or SyntaxKind.FixedStatement
            or SyntaxKind.SizeOfExpression
            or SyntaxKind.PointerIndirectionExpression
            or SyntaxKind.PointerMemberAccessExpression
            or SyntaxKind.AddressOfExpression
            or SyntaxKind.UnsafeStatement;
}
