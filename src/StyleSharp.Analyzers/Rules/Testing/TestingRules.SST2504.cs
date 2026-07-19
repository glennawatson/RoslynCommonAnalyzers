// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2504 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2504 — a class marked as a test fixture declares no test methods.</summary>
    public static readonly DiagnosticDescriptor EmptyTestClass = Create(
        "SST2504",
        "A test class should declare at least one test method",
        "The test class '{0}' declares no test methods, so the runner loads it but never runs anything",
        EmptyTestClassDescription);

    /// <summary>The EmptyTestClass rule description.</summary>
    private const string EmptyTestClassDescription =
        "A class marked as a test fixture — MSTest's test-class attribute or NUnit's test-fixture attribute — tells the runner to load "
        + "and instantiate it, but a fixture that declares no test method has nothing to run: the runner reflects over it, finds no "
        + "test, and moves on. Such a class is almost always a leftover — the tests it once held were deleted or moved, a base class "
        + "was mixed up, or the test attributes were dropped in an edit — and it lingers as dead weight that reads like coverage but "
        + "verifies nothing. Only a concrete class with no test method of its own and no base type that carries tests is reported: an "
        + "abstract fixture is a legitimate shared base, and a fixture that inherits its tests from a base class is exercised through "
        + "the inherited methods. xUnit has no test-class attribute — its test classes are implicit in containing a fact or theory — so "
        + "this rule does not apply to it.";
}
