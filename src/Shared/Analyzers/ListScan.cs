// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Allocation-free existence tests over the list shapes analyzers scan.</summary>
internal static class ListScan
{
    /// <summary>Returns whether any node in a separated list satisfies a test.</summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="nodes">The list to scan.</param>
    /// <param name="matches">The test applied to each node.</param>
    /// <returns><see langword="true"/> at the first node that matches.</returns>
    internal static bool Any<TNode>(in SeparatedSyntaxList<TNode> nodes, Func<TNode, bool> matches)
        where TNode : SyntaxNode
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (matches(nodes[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any array entry satisfies a test against a value.</summary>
    /// <typeparam name="TItem">The entry type.</typeparam>
    /// <typeparam name="TValue">The type of the value each entry is tested against.</typeparam>
    /// <param name="items">The array to scan.</param>
    /// <param name="value">The value passed to every test, so the test captures nothing.</param>
    /// <param name="matches">The test applied to each entry and the value.</param>
    /// <returns><see langword="true"/> at the first entry that matches.</returns>
    internal static bool Any<TItem, TValue>(TItem[] items, TValue value, Func<TItem, TValue, bool> matches)
    {
        for (var i = 0; i < items.Length; i++)
        {
            if (matches(items[i], value))
            {
                return true;
            }
        }

        return false;
    }
}
