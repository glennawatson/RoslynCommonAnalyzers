// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2453 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2453 — a null-coalescing operation whose fallback cannot change the value.</summary>
    public static readonly DiagnosticDescriptor NullCoalesceToNull = Create(
        "SST2453",
        "A coalescing fallback that cannot change the value should be removed",
        "This '??' cannot change the value, so the expression is just its left operand",
        NullCoalesceToNullDescription);

    /// <summary>The NullCoalesceToNull rule description.</summary>
    private const string NullCoalesceToNullDescription =
        "'??' reads as a fallback: keep the left unless it is null, otherwise use the right. Two shapes make that promise "
        + "and keep none of it. When the right operand is a compile-time constant null, the fallback substitutes null for "
        + "null. When the right operand is the left operand written again, it substitutes the value for itself. Either way "
        + "the whole expression is its left operand, and a reader looking for what happens on null finds an answer that was "
        + "never written — usually because the right operand is the mistake, a default that was meant to hold a real value "
        + "or a copy-paste of the thing being tested. The constant-null shape is reported only when the coalescing's own "
        + "type matches the left operand's, so folding cannot change the expression's type the way 'nullable ?? default' "
        + "would; the self-coalescing shape is reported only for a side-effect-free operand, so evaluating it once instead "
        + "of twice changes nothing.";
}
