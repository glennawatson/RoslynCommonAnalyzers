// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Small allocation-free scans over modifier lists for hot analyzer paths.
/// </summary>
internal static class ModifierListHelper
{
    /// <summary>Returns whether <paramref name="modifiers"/> contains <paramref name="kind"/>.</summary>
    /// <param name="modifiers">The modifier list.</param>
    /// <param name="kind">The modifier kind to find.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public static bool Contains(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether <paramref name="modifiers"/> contains either of the requested kinds.</summary>
    /// <param name="modifiers">The modifier list.</param>
    /// <param name="first">The first modifier kind to find.</param>
    /// <param name="second">The second modifier kind to find.</param>
    /// <returns><see langword="true"/> when either modifier is present.</returns>
    public static bool ContainsEither(SyntaxTokenList modifiers, SyntaxKind first, SyntaxKind second)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier.IsKind(first) || modifier.IsKind(second))
            {
                return true;
            }
        }

        return false;
    }
}
