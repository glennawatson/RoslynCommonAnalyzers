// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2462 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2462 — a <c>new</c> member is declared less accessible than the inherited member it hides.</summary>
    public static readonly DiagnosticDescriptor NewMemberReducesAccessibility = Create(
        "SST2462",
        "A member hidden with 'new' should not reduce its accessibility below the member it hides",
        "'{0}' is declared {2} but hides the more accessible {1} '{3}.{0}', which a reference typed as '{3}' still binds to, so the reduced accessibility has no effect",
        NewMemberReducesAccessibilityDescription);

    /// <summary>The NewMemberReducesAccessibility rule description.</summary>
    private const string NewMemberReducesAccessibilityDescription =
        "The 'new' modifier declares a member that hides an inherited one of the same name, but hiding is resolved at compile time "
        + "against the static type of the reference, not the runtime object. Declaring the hiding member with a narrower accessibility "
        + "than the member it hides does not make the inherited member any less reachable: a caller holding the base type still binds to "
        + "the more accessible inherited member, while a caller holding the derived type sees the narrower one. The two members diverge, "
        + "and the accessibility that looks reduced is an illusion. Give the hiding member the same or wider accessibility, or drop the "
        + "'new' member and override or rename it so a single member governs access.";
}
