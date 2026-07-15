// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2416 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2416 — a remainder test against a non-zero value misses negative inputs on a signed type.</summary>
    public static readonly DiagnosticDescriptor SignedRemainderTest = Create(
        "SST2416",
        "A remainder test should account for negative values",
        "'{0}' is signed, so this remainder test is false for every negative value",
        SignedRemainderTestDescription);

    /// <summary>The SignedRemainderTest rule description.</summary>
    private const string SignedRemainderTestDescription =
        "The remainder operator keeps the sign of the dividend, so for a signed operand '-3 % 2' is '-1', not '1'. Comparing the "
        + "remainder against a non-zero constant — the classic 'x % 2 == 1' odd-number test — therefore silently fails for every "
        + "negative value: an odd negative number is never reported as odd. Comparing against zero ('% 2 == 0') is unaffected and is "
        + "not reported. An operand that cannot be negative — a length, a count, an absolute value — is left alone, because the sign "
        + "problem cannot arise there.";
}
