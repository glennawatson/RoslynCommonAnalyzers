// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared primitives for the naming analyzers. The <c>BeginsWith*</c> probes are
/// allocation-free and run on the hot (no-violation) path; the <c>SuggestX</c>
/// transforms allocate a corrected name and are only called once a violation has
/// already been detected, off the hot path.
/// </summary>
internal static class NamingHelper
{
    /// <summary>Returns whether <paramref name="name"/> starts with the capital letter <c>I</c>.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the first character is an upper-case <c>I</c>.</returns>
    public static bool BeginsWithCapitalI(string name) => name.Length > 0 && name[0] == 'I';

    /// <summary>Returns whether <paramref name="name"/> starts with the capital letter <c>T</c>.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the first character is an upper-case <c>T</c>.</returns>
    public static bool BeginsWithCapitalT(string name) => name.Length > 0 && name[0] == 'T';

    /// <summary>Returns whether the first character of <paramref name="name"/> is upper-case.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the first character is upper-case.</returns>
    public static bool BeginsWithUpperCase(string name) => name.Length > 0 && char.IsUpper(name[0]);

    /// <summary>Returns whether the first character of <paramref name="name"/> is lower-case.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the first character is lower-case.</returns>
    public static bool BeginsWithLowerCase(string name) => name.Length > 0 && char.IsLower(name[0]);

    /// <summary>Returns whether <paramref name="name"/> begins with an underscore.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the first character is <c>_</c>.</returns>
    public static bool BeginsWithUnderscore(string name) => name.Length > 0 && name[0] == '_';

    /// <summary>Returns whether <paramref name="name"/> is the runtime private-field form <c>_camelCase</c>.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the name is a single underscore followed by a lower-case letter.</returns>
    public static bool IsUnderscoreCamelCase(string name)
        => name.Length >= 2 && name[0] == '_' && name[1] != '_' && char.IsLower(name[1]);

    /// <summary>Returns whether <paramref name="name"/> consists only of underscores (e.g. <c>_</c>, a discard-style name to skip).</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when every character is an underscore.</returns>
    public static bool IsAllUnderscores(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (c != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Suggests a PascalCase form of <paramref name="name"/> (leading underscores and a known field prefix stripped).</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns>The suggested PascalCase name.</returns>
    public static string SuggestPascalCase(string name)
    {
        var start = SignificantStart(name);
        if (start >= name.Length)
        {
            return name;
        }

        if (start == 0 && char.IsUpper(name[0]))
        {
            return name;
        }

        return char.ToUpperInvariant(name[start]) + name.Substring(start + 1);
    }

    /// <summary>Suggests a camelCase form of <paramref name="name"/> (leading underscores and a known field prefix stripped).</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns>The suggested camelCase name.</returns>
    public static string SuggestCamelCase(string name)
    {
        var start = SignificantStart(name);
        if (start >= name.Length)
        {
            return name;
        }

        if (start == 0 && char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[start]) + name.Substring(start + 1);
    }

    /// <summary>Suggests the runtime private-field form <c>_camelCase</c> for <paramref name="name"/>.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns>The suggested <c>_camelCase</c> name.</returns>
    public static string SuggestUnderscoreCamelCase(string name) => "_" + SuggestCamelCase(name);

    /// <summary>Suggests <paramref name="name"/> with the given single-character prefix (e.g. <c>I</c>, <c>T</c>).</summary>
    /// <param name="name">The identifier text.</param>
    /// <param name="prefix">The prefix character to ensure.</param>
    /// <returns>The prefixed name in PascalCase.</returns>
    public static string SuggestPrefixed(string name, char prefix) => prefix + SuggestPascalCase(name);

    /// <summary>
    /// Returns the index of the first "significant" character — skipping leading
    /// underscores and a leading <c>m_</c>/<c>s_</c>/<c>t_</c> Hungarian/runtime
    /// field prefix — so suggestions don't carry the old prefix forward.
    /// </summary>
    /// <param name="name">The identifier text.</param>
    /// <returns>The index of the first significant character.</returns>
    private static int SignificantStart(string name)
    {
        const int prefixLength = 2;

        var i = 0;
        if (name.Length >= prefixLength && name[1] == '_' && (name[0] == 'm' || name[0] == 's' || name[0] == 't'))
        {
            i = prefixLength;
        }

        while (i < name.Length && name[i] == '_')
        {
            i++;
        }

        return i;
    }
}
