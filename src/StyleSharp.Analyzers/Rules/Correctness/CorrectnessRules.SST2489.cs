// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2489 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2489 — a relational comparison whose result the operand's integer type already fixes.</summary>
    public static readonly DiagnosticDescriptor TypeDecidedComparison = Create(
        "SST2489",
        "Relational comparison is decided by the operand's type",
        "'{0}' {1}",
        TypeDecidedComparisonDescription);

    /// <summary>The TypeDecidedComparison rule description.</summary>
    private const string TypeDecidedComparisonDescription =
        "An integer type carries a fixed range, so a relational comparison against that range's edge is decided before it runs. "
        + "An unsigned value is never negative, so 'x >= 0' is always true and 'x < 0' is always false; 'x > 0' can only be false "
        + "at zero, so it is an '!= 0' test written as a bounds check. A value compared to its own type's maximum is settled the "
        + "same way — 'b <= 255' is always true for a 'byte', 'b > 255' always false. These read as real range checks but guard "
        + "nothing, so a validation that was meant to reject out-of-range input silently passes, or a branch that was meant to run "
        + "never does. Only a comparison whose constant sits exactly on the operand type's minimum or maximum is reported: a constant "
        + "past that edge is already a compiler diagnostic, and an interior constant asks a real question. Floating-point operands are "
        + "left alone — 'NaN' makes even a self-comparison meaningful — as are signed values compared to zero, which is a genuine test.";
}
