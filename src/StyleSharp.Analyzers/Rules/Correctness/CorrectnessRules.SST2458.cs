// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2458 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2458 — a bitwise operation treats a non-flags enum's values as independent bits.</summary>
    public static readonly DiagnosticDescriptor NonFlagsEnumBitwise = Create(
        "SST2458",
        "Bitwise operations should not be applied to a non-flags enum",
        "'{0}' is not a flags enum; '{1}' treats its values as independent bits and can produce a value that is not any of its members",
        NonFlagsEnumBitwiseDescription);

    /// <summary>The NonFlagsEnumBitwise rule description.</summary>
    private const string NonFlagsEnumBitwiseDescription =
        "An enum without [Flags] is a list of alternatives whose values the compiler numbers sequentially, so its members share "
        + "bits instead of owning one each. A bitwise operation on such values lands on a different member, or on no member at "
        + "all, and a masked test answers a question about bits nobody assigned — while everything still compiles and runs. If "
        + "the values really do combine, declare the enum as a flag set and give each member its own bit; if they do not, "
        + "compare the values instead of combining them.";
}
