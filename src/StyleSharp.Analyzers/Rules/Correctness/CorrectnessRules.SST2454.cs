// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2454 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2454 — the result of an <c>as</c> conversion is dereferenced without a null check.</summary>
    public static readonly DiagnosticDescriptor UncheckedAsDereference = Create(
        "SST2454",
        "Do not dereference the result of an 'as' conversion",
        "'as' yields null when the conversion fails, so this dereference throws instead of converting",
        UncheckedAsDereferenceDescription);

    /// <summary>The UncheckedAsDereference rule description.</summary>
    private const string UncheckedAsDereferenceDescription =
        "The whole point of 'as' over a cast is that a failed conversion yields null instead of throwing. "
        + "Dereferencing that result immediately gives back the throw — but as a NullReferenceException at the "
        + "member access, not an InvalidCastException naming the type that did not match, so the failure reads "
        + "as a missing value rather than a wrong type. Either the conversion cannot fail, in which case a cast "
        + "says so and fails with the right exception, or it can, in which case the result needs a null check, "
        + "a conditional access, or an 'is' pattern that binds only on success. Reported for a direct "
        + "dereference — a member access, an index, or a call — on the result; a conditional access is exactly "
        + "the null check this asks for and is left alone.";
}
