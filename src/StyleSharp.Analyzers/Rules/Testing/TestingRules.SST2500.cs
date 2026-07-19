// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2500 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2500 — a test method contains no assertion and no expected-exception check.</summary>
    public static readonly DiagnosticDescriptor TestAssertsNothing = Create(
        "SST2500",
        "A test method should verify something",
        "The test method '{0}' contains no assertion and no expected-exception check, so it always passes without verifying anything",
        TestAssertsNothingDescription);

    /// <summary>The TestAssertsNothing rule description.</summary>
    private const string TestAssertsNothingDescription =
        "A test method carrying a test attribute that never asserts and declares no expected exception runs, passes, and "
        + "verifies nothing: a regression in the code it appears to cover slips through green. To keep false positives near "
        + "zero the rule reports only when it can prove the body verifies nothing. Every invocation and object creation in "
        + "the body must resolve to a non-verifying platform (BCL) API — the in-BCL Debug, Trace, and Contract assertion "
        + "helpers excepted — or the body must contain none at all. Any call the rule cannot prove is a harmless platform "
        + "call — a call into the user's own code that might be an assertion helper, a third-party or assertion-framework "
        + "call, a throw, or an unresolved call — leaves the method silent, because proving it verifies nothing would need "
        + "interprocedural analysis. Add an assertion, an expected-exception check, or remove the empty test.";
}
