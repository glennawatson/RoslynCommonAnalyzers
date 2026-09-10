// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2337 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2337 — an assembly-internal abstract base whose descendants are all known could be <c>closed</c>.</summary>
    public static readonly DiagnosticDescriptor PreferClosedHierarchy = CreateDisabled(
        "SST2337",
        "Declare a fully known hierarchy as closed",
        "Mark '{0}' as 'closed' so the compiler can check a switch over its descendants for exhaustiveness",
        PreferClosedHierarchyDescription);

    /// <summary>The PreferClosedHierarchy rule description.</summary>
    private const string PreferClosedHierarchyDescription =
        "A 'closed' class can only be derived from inside the assembly that declares it, which fixes the set of direct "
        + "descendants at compile time. That is what lets a switch over the hierarchy be exhaustive without a default arm: "
        + "the compiler knows every case, so adding a descendant later turns every switch that has not been updated into a "
        + "warning instead of a silent fall-through to the default. Reported for an abstract class or record that is not "
        + "reachable from outside its assembly and already has two or more direct descendants in the compilation, so the "
        + "modifier states a restriction that is true either way. An externally visible type is never reported, because "
        + "there 'closed' forbids derivation that callers may already rely on -- a breaking change rather than a tidy-up. "
        + "Off by default: adopting the modifier is a deliberate choice about how a hierarchy is meant to be extended.";
}
