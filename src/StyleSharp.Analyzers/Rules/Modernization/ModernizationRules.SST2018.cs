// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2018 descriptor.</summary>
internal static partial class ModernizationRules
{
    /// <summary>SST2018 — a null check sits beside a type pattern that already covers null.</summary>
    public static readonly DiagnosticDescriptor RedundantNullCheckBesidePattern = Create(
        "SST2018",
        "A null check beside a type pattern is redundant",
        "The '{0}' type pattern already excludes null, so this null check is redundant",
        RedundantNullCheckBesidePatternDescription);

    /// <summary>The RedundantNullCheckBesidePattern rule description.</summary>
    private const string RedundantNullCheckBesidePatternDescription =
        "An explicit null test is combined with an 'is' type pattern on the same value: 'o != null && o is T', 'o is not null && o is "
        + "T', or the combinator form 'o is not null and T'. The type pattern already answers the null question — 'o is T' is false for "
        + "a null 'o' for every 'T', including a nullable value type whose 'HasValue' is false — so the null test decides nothing and "
        + "can be dropped to leave 'o is T'. A test that keeps a non-null value which is not a 'T' ('o != null && o is not T') is a "
        + "different, real, check and is left alone.";
}
