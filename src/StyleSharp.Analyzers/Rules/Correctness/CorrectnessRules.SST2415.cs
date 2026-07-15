// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2415 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2415 — a non-short-circuiting boolean operator runs work the left operand was meant to guard.</summary>
    public static readonly DiagnosticDescriptor NonShortCircuitGuard = Create(
        "SST2415",
        "A boolean guard should short-circuit",
        "'{0}' always evaluates its right operand; the left operand cannot guard the work on the right",
        NonShortCircuitGuardDescription);

    /// <summary>The NonShortCircuitGuard rule description.</summary>
    private const string NonShortCircuitGuardDescription =
        "A single '&' or '|' between two boolean operands evaluates both sides every time, and here the right side does real work — a "
        + "call, an assignment, an increment, an element access, an object creation. When the left operand is a null test or a "
        + "readiness check, it reads like a guard, but the eager operator ignores it: the right side runs even when the left decided it "
        + "should not. Writing '&&' or '||' makes the guard hold. This differs from an untidy eager operator over two plain reads, "
        + "where switching to the short-circuiting form changes nothing observable; here the right operand's work is exactly what the "
        + "guard was supposed to prevent, so replacing the operator changes behaviour.";
}
