// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2508 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2508 — a fluent assertion names a subject with <c>Should()</c> but states no check.</summary>
    public static readonly DiagnosticDescriptor IncompleteAssertion = Create(
        "SST2508",
        "A fluent assertion is started but never completed",
        "This 'Should()' statement names a subject but states no check, so the test verifies nothing; complete it with an assertion such as 'Be(...)'",
        IncompleteAssertionDescription);

    /// <summary>The IncompleteAssertion rule description.</summary>
    private const string IncompleteAssertionDescription =
        "A fluent assertion begins by naming its subject with 'value.Should()' and only asserts once a check — 'Be', 'Contain', "
        + "'BeTrue', and the like — is chained onto it. A 'value.Should();' written as a whole statement stops at the subject: it "
        + "compiles, runs, and passes while checking nothing, so a regression in the code it appears to cover slips through green. "
        + "The rule reports only this proven-incomplete shape: an expression statement whose whole expression is a 'Should()' "
        + "invocation with nothing chained after it, and only once the call binds to a subject type from a fluent-assertion "
        + "library (its type's root namespace is 'FluentAssertions' or 'AwesomeAssertions'). A completed 'value.Should().Be(1)' "
        + "chains a check and is never reported, and a 'Should' from any other library is left alone. Add the missing check or "
        + "remove the dangling statement.";
}
