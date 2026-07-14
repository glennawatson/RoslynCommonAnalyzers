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

    /// <summary>
    /// Returns whether <paramref name="name"/> is PascalCase all the way through, not merely
    /// upper-case in its first character (which is all <see cref="BeginsWithUpperCase"/> asks).
    /// A conforming name starts upper-case, holds no underscore, and capitalizes acronyms the way
    /// the .NET guidelines do: a two-letter acronym stays upper (<c>IOMode</c>, <c>MyIO</c>), and a
    /// longer one is written as a word (<c>HttpStatus</c>, not <c>HTTPStatus</c>).
    /// </summary>
    /// <param name="name">The identifier text.</param>
    /// <returns><see langword="true"/> when the name conforms.</returns>
    public static bool IsPascalCase(string name)
    {
        if (!BeginsWithUpperCase(name))
        {
            return false;
        }

        var i = 0;
        while (i < name.Length)
        {
            if (name[i] == '_')
            {
                return false;
            }

            if (!char.IsUpper(name[i]))
            {
                i++;
                continue;
            }

            var run = UpperCaseRunLength(name, i);
            if (run > MaximumRunLength(name, i, run))
            {
                return false;
            }

            i += run;
        }

        return true;
    }

    /// <summary>
    /// Suggests a fully PascalCase form of <paramref name="name"/>: underscores are dropped and the
    /// word they separated is capitalized, and an acronym longer than two letters becomes a word
    /// (<c>HTTPStatus</c> to <c>HttpStatus</c>, <c>MyENUM</c> to <c>MyEnum</c>). Unlike
    /// <see cref="SuggestPascalCase"/>, which only reaches the first character, the result is
    /// guaranteed to satisfy <see cref="IsPascalCase"/>. Called only after a violation is found.
    /// </summary>
    /// <param name="name">The identifier text.</param>
    /// <returns>The suggested PascalCase name.</returns>
    public static string SuggestPascalCaseName(string name)
    {
        var start = SignificantStart(name);
        if (start >= name.Length)
        {
            return name;
        }

        var buffer = new char[name.Length - start];
        var length = 0;
        var i = start;
        while (i < name.Length)
        {
            var current = name[i];
            if (current == '_')
            {
                i++;
                continue;
            }

            if (char.IsUpper(current))
            {
                var run = UpperCaseRunLength(name, i);
                AppendRun(buffer, ref length, name, i, run);
                i += run;
                continue;
            }

            var opensAWord = length == 0 || name[i - 1] == '_';
            buffer[length++] = opensAWord ? char.ToUpperInvariant(current) : current;
            i++;
        }

        return length == 0 ? name : new string(buffer, 0, length);
    }

    /// <summary>Suggests a PascalCase form of <paramref name="name"/> (leading underscores and a known field prefix stripped).</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns>The suggested PascalCase name.</returns>
    public static string SuggestPascalCase(string name)
    {
        var start = SignificantStart(name);
        if (start >= name.Length || (start == 0 && char.IsUpper(name[0])))
        {
            return name;
        }

        return char.ToUpperInvariant(name[start]) + name[(start + 1)..];
    }

    /// <summary>Suggests a camelCase form of <paramref name="name"/> (leading underscores and a known field prefix stripped).</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns>The suggested camelCase name.</returns>
    public static string SuggestCamelCase(string name)
    {
        var start = SignificantStart(name);
        return start >= name.Length || (start == 0 && char.IsLower(name[0]))
            ? name
            : char.ToLowerInvariant(name[start]) + name[(start + 1)..];
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

    /// <summary>Returns the length of the run of upper-case letters starting at <paramref name="start"/>.</summary>
    /// <param name="name">The identifier text.</param>
    /// <param name="start">The index of the first upper-case letter of the run.</param>
    /// <returns>The number of consecutive upper-case letters.</returns>
    private static int UpperCaseRunLength(string name, int start)
    {
        var end = start;
        while (end < name.Length && char.IsUpper(name[end]))
        {
            end++;
        }

        return end - start;
    }

    /// <summary>
    /// Returns how long a run of capitals may be before it stops being PascalCase. An acronym runs
    /// to two letters (<c>MyIO</c>), so a run that ends the name may be two; a run that is followed
    /// by a word may be three, because its last capital opens that word (<c>IOMode</c>).
    /// </summary>
    /// <param name="name">The identifier text.</param>
    /// <param name="start">The index the run starts at.</param>
    /// <param name="runLength">The length of the run.</param>
    /// <returns>The longest run permitted in this position.</returns>
    private static int MaximumRunLength(string name, int start, int runLength)
    {
        const int trailingAcronym = 2;
        const int acronymAndWordInitial = 3;

        return start + runLength >= name.Length ? trailingAcronym : acronymAndWordInitial;
    }

    /// <summary>
    /// Copies one run of capitals into the suggestion, rewriting an acronym that is too long as a
    /// word: the initial stays upper, the rest of the acronym goes lower, and the capital that opens
    /// the following word (when there is one) is kept.
    /// </summary>
    /// <param name="buffer">The suggestion being built.</param>
    /// <param name="length">The number of characters written so far.</param>
    /// <param name="name">The identifier text.</param>
    /// <param name="start">The index the run starts at.</param>
    /// <param name="runLength">The length of the run.</param>
    private static void AppendRun(char[] buffer, ref int length, string name, int start, int runLength)
    {
        var end = start + runLength;
        if (runLength <= MaximumRunLength(name, start, runLength))
        {
            for (var i = start; i < end; i++)
            {
                buffer[length++] = name[i];
            }

            return;
        }

        var wordFollows = end < name.Length;
        var acronymEnd = wordFollows ? end - 1 : end;

        buffer[length++] = name[start];
        for (var i = start + 1; i < acronymEnd; i++)
        {
            buffer[length++] = char.ToLowerInvariant(name[i]);
        }

        if (!wordFollows)
        {
            return;
        }

        buffer[length++] = name[end - 1];
    }

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
