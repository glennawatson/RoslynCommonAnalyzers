// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2497 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2497 — an expression-bodied member forwards to itself, so every call recurses forever.</summary>
    public static readonly DiagnosticDescriptor SelfRecursiveForwarder = Create(
        "SST2497",
        "Do not forward a member to itself",
        "'{0}' forwards to itself with its own parameters, so every call recurses until the stack overflows",
        SelfRecursiveForwarderDescription);

    /// <summary>The SelfRecursiveForwarder rule description.</summary>
    private const string SelfRecursiveForwarderDescription =
        "An expression-bodied member whose body calls its own name, handing every parameter straight back in its own "
        + "position, binds to itself: an exact signature match beats every overload that would need a conversion. The "
        + "call therefore recurses with identical arguments and never terminates, so the first caller gets a "
        + "StackOverflowException that cannot be caught. This is what a forwarder looks like when the intended target "
        + "was an overload, a base implementation, or a differently named member, and the wrong one was reached because "
        + "the names match. Call the member that was meant instead — a base implementation is reached with base., and an "
        + "overload needs an argument list that actually selects it.";
}
