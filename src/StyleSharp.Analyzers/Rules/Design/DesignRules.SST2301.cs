// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2301 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2301 — a type that signs the equality contract can still be derived from.</summary>
    public static readonly DiagnosticDescriptor EquatableTypeShouldBeSealed = Create(
        "SST2301",
        "Types implementing IEquatable<T> should be sealed",
        "'{0}' implements IEquatable<{0}> but is not sealed; a derived type cannot honour it",
        EquatableTypeShouldBeSealedDescription);

    /// <summary>The EquatableTypeShouldBeSealed rule description.</summary>
    private const string EquatableTypeShouldBeSealedDescription =
        "'IEquatable<T>' promises that equality is decided against exactly 'T'. A derived type breaks that promise the moment it exists: "
        + "'base.Equals(derived)' can answer true while 'derived.Equals(base)' answers false, so equality stops being symmetric and every "
        + "hash-based collection quietly misbehaves. Seal the type, or move equality to a contract a hierarchy can actually keep.";
}
