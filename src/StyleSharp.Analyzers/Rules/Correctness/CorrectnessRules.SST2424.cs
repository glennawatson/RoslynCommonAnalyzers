// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2424 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2424 — an override declares a different parameter default than the base.</summary>
    public static readonly DiagnosticDescriptor OverrideChangesDefault = Create(
        "SST2424",
        "An override should not change a parameter's default value",
        "'{0}' has a different default value here than the base declares; a call through the base type uses the base's value, because a default binds to the receiver's static type",
        OverrideChangesDefaultDescription);

    /// <summary>The OverrideChangesDefault rule description.</summary>
    private const string OverrideChangesDefaultDescription =
        "A default value is not stored on the method - it is chosen at the call site from the static type of the receiver. When an override "
        + "declares a default that differs from the base (a different value, one the base does not have, or dropping one the base does), the "
        + "same object called through the base type and through the derived type receives different arguments, silently. Match the base's "
        + "default so a call means the same thing whichever reference type holds the object.";
}
