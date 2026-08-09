// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2453 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2453 — the right of a null-coalescing operation is a constant null, so the operator changes nothing.</summary>
    public static readonly DiagnosticDescriptor NullCoalesceToNull = Create(
        "SST2453",
        "Coalescing to null leaves the value unchanged",
        "The right of '??' is always null, so this expression is just its left operand",
        NullCoalesceToNullDescription);

    /// <summary>The NullCoalesceToNull rule description.</summary>
    private const string NullCoalesceToNullDescription =
        "'??' reads as a fallback: keep the left unless it is null, otherwise use the right. When the right operand is a "
        + "compile-time constant null, the fallback substitutes null for null and the whole expression is its left operand. "
        + "The operator advertises a default that does not exist, so a reader looking for what happens on null finds an "
        + "answer that was never written. Usually the right operand is the mistake — a default that was meant to hold a "
        + "real value, or a leftover from a rewrite. Reported only when the coalescing's own type matches the left "
        + "operand's, so folding to the left cannot change the expression's type the way 'nullable ?? default' would.";
}
