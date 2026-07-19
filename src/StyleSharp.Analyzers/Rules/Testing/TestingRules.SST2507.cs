// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2507 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2507 — a test method uses an expected-exception attribute instead of asserting the throwing operation.</summary>
    public static readonly DiagnosticDescriptor ExpectedException = Create(
        "SST2507",
        "A test's expected exception should be asserted on the specific operation",
        "This test's expected-exception attribute passes if any statement in the method throws, not the operation under test; assert the specific call with 'Assert.Throws' instead",
        ExpectedExceptionDescription);

    /// <summary>The ExpectedException rule description.</summary>
    private const string ExpectedExceptionDescription =
        "An expected-exception attribute on a test method asserts only that some statement, anywhere in the whole method body, threw the "
        + "named exception. It does not say which statement threw, and it does not say the operation under test was the one that threw — a "
        + "setup line, an arrange step, or a mistyped call that throws the same exception type passes the test just as well, so the test keeps "
        + "reporting green while asserting nothing about the behaviour it was written to pin down. The precise form wraps the single operation "
        + "expected to fail in 'Assert.Throws<T>(() => operation)', which asserts that that call, and only that call, threw. No code fix is "
        + "offered: turning the attribute into an assertion means choosing which statement in the body is the one expected to throw, and only "
        + "the test's author knows that.";
}
