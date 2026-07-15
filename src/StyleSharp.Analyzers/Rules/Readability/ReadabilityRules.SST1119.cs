// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST1119 descriptor.</summary>
internal static partial class ReadabilityRules
{
    /// <summary>SST1119 — a numeric literal's digit separators group its digits irregularly.</summary>
    public static readonly DiagnosticDescriptor IrregularDigitGrouping = Create(
        "SST1119",
        "Digit separators should group digits regularly",
        "'{0}' groups its digits irregularly; group them evenly for the base",
        IrregularDigitGroupingDescription);

    /// <summary>The IrregularDigitGrouping rule description.</summary>
    private const string IrregularDigitGroupingDescription =
        "A numeric literal uses '_' digit separators, but the groups after the first are not all the same width, or that width is not "
        + "the convention for the base (three for decimal, four or two for hexadecimal and binary). Uneven grouping is a misreading "
        + "hazard, not just an untidy one: '1_000_00_000' looks like a billion and is a hundred million. The leading group may be "
        + "short, so '1_000_000' and '0x_FF_FF' are both fine. This is disjoint from the separate rule that reports a long literal "
        + "carrying no separators at all.";
}
