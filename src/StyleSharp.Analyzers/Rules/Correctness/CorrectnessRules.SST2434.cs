// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2434 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2434 — a reference-type array is widened to an array of its base type.</summary>
    public static readonly DiagnosticDescriptor ArrayCovariance = Create(
        "SST2434",
        "Avoid array covariance",
        "converting '{0}' to '{1}' makes every element write a runtime-checked store that can throw; {2}",
        ArrayCovarianceDescription);

    /// <summary>The ArrayCovariance rule description.</summary>
    private const string ArrayCovarianceDescription =
        "A reference-type array converts implicitly to an array of any of its element type's base types, so a string[] can "
        + "be handed over as an object[]. The conversion compiles without a warning, but the runtime now checks every write "
        + "through that reference against the real element type and throws ArrayTypeMismatchException on a mismatch: "
        + "'object[] items = new string[3]; items[0] = 42;' fails at run time, not compile time. Pass the array as an "
        + "IReadOnlyList<T> (or a ReadOnlySpan<T> where one is available) when the receiver only reads it, or keep it typed "
        + "as the concrete array so no widening happens.";
}
