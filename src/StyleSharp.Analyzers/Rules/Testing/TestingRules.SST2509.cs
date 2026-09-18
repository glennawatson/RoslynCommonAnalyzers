// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2509 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2509 — a test method's signature prevents the runner from executing it.</summary>
    public static readonly DiagnosticDescriptor InvalidTestMethodShape = Create(
        "SST2509",
        "A test method has a shape the runner cannot execute",
        InvalidTestMethodShapeMessage,
        InvalidTestMethodShapeDescription);

    /// <summary>The InvalidTestMethodShape message format.</summary>
    private const string InvalidTestMethodShapeMessage =
        "The test method '{0}' {1}; use a signature supported by its test framework";

    /// <summary>The InvalidTestMethodShape rule description.</summary>
    private const string InvalidTestMethodShapeDescription =
        "Test methods must be public for xUnit, NUnit, and MSTest, and generic methods need parameters from which to infer "
        + "their types. Supported return types are void, Task, ValueTask, Task<T>, and ValueTask<T>. NUnit can also consume "
        + "ordinary return values through ExpectedResult on each TestCase, ExpectedResult on a parameterless Test, or expected "
        + "results supplied by TestCaseSource. Static methods are allowed. Invalid signatures can prevent discovery or execution; "
        + "check the requirements of the framework and runner version.";
}
