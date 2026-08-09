// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2447 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2447 — an integer difference compared against zero is not the comparison it reads as.</summary>
    public static readonly DiagnosticDescriptor DifferenceComparedToZero = Create(
        "SST2447",
        "Compare the operands instead of their difference",
        "Compare the operands directly: '{0}' is not the same test as subtracting and comparing to zero",
        DifferenceComparedToZeroDescription);

    /// <summary>The DifferenceComparedToZero rule description.</summary>
    private const string DifferenceComparedToZeroDescription =
        "'a - b > 0' reads as 'a > b', but integer subtraction wraps. When the difference does not fit the operand "
        + "type the wrapped value lands on the wrong side of zero and the test returns the opposite answer — and on an "
        + "unsigned type it is worse, because every case where b exceeds a wraps to a large positive number and the "
        + "test says 'greater' for every pair it should reject. Comparing the operands has no intermediate value to "
        + "overflow and is right for every input. Reported for a subtraction of two integer operands compared against "
        + "the literal zero, in either operand position; floating-point and decimal arithmetic have no wrapping "
        + "difference to misread and are left alone.";
}
