// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Allocation-free primitives shared by the naming analyzers. Everything operates
/// directly on the identifier text so no substrings or temporaries are produced on
/// the hot path.
/// </summary>
internal static class NamingHelper
{
    /// <summary>Returns whether <paramref name="name"/> starts with the capital letter <c>I</c>.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the first character is an upper-case <c>I</c>.</returns>
    public static bool BeginsWithCapitalI(string name) => name.Length > 0 && name[0] == 'I';

    /// <summary>Returns whether the first character of <paramref name="name"/> is an upper-case letter.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the first character is upper-case.</returns>
    public static bool BeginsWithUpperCase(string name) => name.Length > 0 && char.IsUpper(name[0]);

    /// <summary>Returns whether the first character of <paramref name="name"/> is a lower-case letter.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the first character is lower-case.</returns>
    public static bool BeginsWithLowerCase(string name) => name.Length > 0 && char.IsLower(name[0]);
}
