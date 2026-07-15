// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2420 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2420 — an index-of result is tested with a strict comparison that skips index 0.</summary>
    public static readonly DiagnosticDescriptor IndexOfSkipsFirst = Create(
        "SST2420",
        "An index-of test should not skip the first position",
        "'{0}' returns 0 for a match at the first position, so '> 0' misses it",
        IndexOfSkipsFirstDescription);

    /// <summary>The IndexOfSkipsFirst rule description.</summary>
    private const string IndexOfSkipsFirstDescription =
        "A search that returns the index of a match, then tests that index with '> 0', treats a match at position 0 as though nothing "
        + "was found. The search returns 0 for the first element and -1 for no match, so the test that answers 'was it found' is '>= 0' "
        + "or '!= -1'; '> 0' quietly excludes the first position. The habit comes from C, where a non-zero return meant success. A "
        + "membership test is clearest written as 'Contains'. The '>= 1' form is left alone: it is the same expression written on "
        + "purpose, a deliberate 'found beyond the first position'.";
}
