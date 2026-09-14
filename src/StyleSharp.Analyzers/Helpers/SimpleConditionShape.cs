// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Classifies loop conditions built only from names, literals and operators.</summary>
internal static class SimpleConditionShape
{
    /// <summary>Returns whether a condition holds only names, literals and non-stepping operators, and reads a bounded number of names.</summary>
    /// <param name="condition">The loop's condition.</param>
    /// <param name="maximumVariables">The most names the condition may read.</param>
    /// <returns><see langword="true"/> when the condition reads between one and <paramref name="maximumVariables"/> names and nothing else can change its value.</returns>
    internal static bool IsSimple(ExpressionSyntax condition, int maximumVariables)
    {
        var identifiers = 0;
        return VisitNode(condition, ref identifiers)
            && DescendantTraversalHelper.VisitDescendants<SyntaxNode, int>(condition, ref identifiers, VisitNode)
            && identifiers > 0
            && identifiers <= maximumVariables;
    }

    /// <summary>Counts a name, or rejects anything that is not a literal or a non-stepping operator.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="identifiers">The number of names read so far.</param>
    /// <returns><see langword="false"/> once the condition is rejected, which stops the walk.</returns>
    private static bool VisitNode(SyntaxNode node, ref int identifiers)
    {
        if (node is IdentifierNameSyntax)
        {
            identifiers++;
            return true;
        }

        return node is LiteralExpressionSyntax
            or ParenthesizedExpressionSyntax
            or BinaryExpressionSyntax
            or PrefixUnaryExpressionSyntax { RawKind: not ((int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression) };
    }
}
