// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2502 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2502 — an equality assertion is given a computed value as its expected argument and a constant as its actual.</summary>
    public static readonly DiagnosticDescriptor ReversedEqualityAssertion = Create(
        "SST2502",
        "The expected value should be the first argument to an equality assertion",
        "A constant is passed as the actual value and a computed value as the expected value, so a failure reports them the wrong way round; put the expected value first",
        ReversedEqualityAssertionDescription);

    /// <summary>The ReversedEqualityAssertion rule description.</summary>
    private const string ReversedEqualityAssertionDescription =
        "An equality assertion takes the expected value first and the actual value second, and prints the failure as "
        + "\"expected <first>, actual <second>\". When the second argument is a compile-time constant and the first is a computed "
        + "value, the two are almost certainly reversed: the constant is the value the test knows in advance, so it belongs in the "
        + "expected slot, while the computed value is what the code produced and belongs in the actual slot. Left reversed, a failing "
        + "test blames the wrong side — it claims it expected the value the code produced and got the constant the test was written "
        + "around — which is backwards and sends the reader looking in the wrong place. The rule is deliberately narrow to avoid "
        + "flagging correct tests: it reports only when the actual (second) argument is a constant, literal, 'nameof', or other "
        + "compile-time constant and the expected (first) argument is not a constant. When both arguments are constants, or both are "
        + "computed, or the constant is already first, nothing is reported, because the common and correct shape puts the constant "
        + "first and must never be flagged.";
}
