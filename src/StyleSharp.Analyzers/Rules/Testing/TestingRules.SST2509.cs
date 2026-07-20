// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2509 descriptor.</summary>
internal static partial class TestingRules
{
    /// <summary>SST2509 — a test method's signature stops the runner executing it, so it is silently skipped.</summary>
    public static readonly DiagnosticDescriptor InvalidTestMethodShape = Create(
        "SST2509",
        "A test method has a shape the runner cannot execute",
        InvalidTestMethodShapeMessage,
        InvalidTestMethodShapeDescription);

    /// <summary>The InvalidTestMethodShape message format.</summary>
    private const string InvalidTestMethodShapeMessage =
        "The test method '{0}' {1}, so the runner cannot execute it and silently skips it; make it public, non-generic, and "
        + "return void, Task, or ValueTask";

    /// <summary>The InvalidTestMethodShape rule description.</summary>
    private const string InvalidTestMethodShapeDescription =
        "A method that carries a test attribute but whose signature the runner cannot execute is still discovered as a test and "
        + "then silently skipped: it looks green in every report because it never runs and its assertions are never reached. Three "
        + "shapes are reported, each required across the common frameworks. The method must be public — a non-public test method is "
        + "not discovered by xUnit, NUnit, or MSTest. It must not be a generic method with no parameters — a parameterless generic "
        + "test has no argument from which its type parameter can be inferred, so no framework can close and run it. And it must "
        + "return void or an awaitable the runner awaits — void, Task, ValueTask, Task<T>, or ValueTask<T> — because any other "
        + "return value is left unobserved and the case does not run. A static method is not reported, because some frameworks run "
        + "static test methods. The rule is gated on at least one test attribute resolving in the compilation, binds only methods "
        + "that already carry a known test-attribute name, and confirms the attribute is a real test before reporting, so a "
        + "same-named attribute from an unrelated namespace is never treated as a test.";
}
