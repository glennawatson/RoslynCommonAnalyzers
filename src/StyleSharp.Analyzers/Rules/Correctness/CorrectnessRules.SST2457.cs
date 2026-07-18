// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2457 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2457 — an unchecked wrapper around a sequence sum that still throws on overflow.</summary>
    public static readonly DiagnosticDescriptor UncheckedSequenceSum = Create(
        "SST2457",
        "Wrapping a sequence sum in unchecked does not stop it throwing",
        "The integer Sum operator checks for overflow internally, so the enclosing 'unchecked' does not stop it throwing",
        UncheckedSequenceSumDescription);

    /// <summary>The UncheckedSequenceSum rule description.</summary>
    private const string UncheckedSequenceSumDescription =
        "An unchecked context is lexical: it changes the arithmetic written inside it, not the arithmetic inside the methods it calls. "
        + "The integer overloads of the sequence Sum operator accumulate in a checked context of their own, so the call throws "
        + "OverflowException on overflow no matter what wraps it — the wrapper documents a wraparound the code does not deliver. "
        + "To really wrap around, accumulate in your own loop inside the unchecked context; to make overflow survivable instead, "
        + "sum into a wider type such as long.";
}
