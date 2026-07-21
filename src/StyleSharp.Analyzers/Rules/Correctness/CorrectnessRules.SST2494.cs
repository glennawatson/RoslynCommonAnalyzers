// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2494 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2494 — the left of a null-coalescing operation is a constant null, so the right is always taken.</summary>
    public static readonly DiagnosticDescriptor ConstantNullCoalesce = Create(
        "SST2494",
        "Null-coalescing over a constant null always takes the right operand",
        "The left of '??' is always null, so this expression always evaluates to its right operand",
        ConstantNullCoalesceDescription);

    /// <summary>The ConstantNullCoalesce rule description.</summary>
    private const string ConstantNullCoalesceDescription =
        "The left operand of a '??' is a compile-time constant null — the null literal, a 'default' of a reference or "
        + "nullable type, or a constant whose value is null — so the coalescing can never choose it and the right operand "
        + "is taken every time. The operator reads as a fallback but guards nothing; usually the left operand is a mistake "
        + "(a constant that was meant to hold a real value, or a stray 'default'). The expression is equivalent to its "
        + "right operand, so the fix replaces the whole thing with that operand. If the left was supposed to be a live "
        + "value, the real repair is to give it one.";
}
