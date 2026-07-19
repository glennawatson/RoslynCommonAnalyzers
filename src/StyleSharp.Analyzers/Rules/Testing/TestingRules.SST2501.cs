// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2501 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2501 — an equality or identity assertion compares an expression with itself.</summary>
    public static readonly DiagnosticDescriptor SelfComparisonAssertion = Create(
        "SST2501",
        "An equality or identity assertion should not compare an expression with itself",
        "This assertion's two operands are the same expression, so it {0}",
        SelfComparisonAssertionDescription);

    /// <summary>The SelfComparisonAssertion rule description.</summary>
    private const string SelfComparisonAssertionDescription =
        "An equality or identity assertion whose two operands are the same expression tests nothing about the code: "
        + "because both sides evaluate the same reference, a positive assertion (Equal, StrictEqual, AreEqual, Same, AreSame, "
        + "or That with EqualTo/SameAs) passes for every possible value, and a negative assertion (NotEqual, NotSame, AreNotEqual, "
        + "AreNotSame) fails for every possible value. Either way the assertion cannot distinguish a correct result from a wrong "
        + "one, which usually means one operand was meant to be a different value — the expected constant, a second instance, or "
        + "the result of a separate call. The two operands are compared syntactically, and a call, object creation, await, or "
        + "assignment on either side is never reported, because two such expressions need not evaluate to the same value.";
}
