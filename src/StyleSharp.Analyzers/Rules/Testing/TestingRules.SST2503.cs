// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2503 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2503 — a boolean literal handed to an equality assertion should use the boolean assertion.</summary>
    public static readonly DiagnosticDescriptor BooleanLiteralAssertion = Create(
        "SST2503",
        "An equality assertion against a boolean literal should use the boolean assertion",
        "This assertion compares a value against a boolean literal; call '{0}' instead so a failure reports the condition, not a value mismatch",
        BooleanLiteralAssertionDescription);

    /// <summary>The BooleanLiteralAssertion rule description.</summary>
    private const string BooleanLiteralAssertionDescription =
        "An equality assertion given a boolean literal as one operand — comparing a value to 'true' or 'false' — states its intent "
        + "through the wrong shape. The dedicated boolean assertion ('IsTrue'/'IsFalse', or 'True'/'False') says directly that the value "
        + "under test must hold, and when it fails it reports the condition rather than an expected-versus-actual mismatch that reads as a "
        + "plain true/false comparison and hides what was really being checked. A 'true' literal maps to the affirmative assertion and a "
        + "'false' literal to its negation. The rule fires only when the call belongs to a recognised test framework's 'Assert' type, the "
        + "other operand is itself a boolean value, and the framework exposes the boolean assertion to switch to; a code fix rewrites the "
        + "call, dropping the literal and keeping the value under test.";
}
