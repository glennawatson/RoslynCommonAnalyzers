// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2426 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2426 — an override's <c>params</c> modifier disagrees with the base and is ignored.</summary>
    public static readonly DiagnosticDescriptor OverrideChangesParams = Create(
        "SST2426",
        "An override should keep the base's params modifier",
        "'{0}' has a 'params' modifier that disagrees with the base; an override's 'params' modifier is ignored, so it only misleads a reader about how callers of this type behave",
        OverrideChangesParamsDescription);

    /// <summary>The OverrideChangesParams rule description.</summary>
    private const string OverrideChangesParamsDescription =
        "Whether a call expands its trailing arguments into an array is decided entirely by the declaration being overridden - an override's own "
        + "'params' modifier has no effect. So an override that adds 'params' the base lacks does not let callers of the derived type pass an "
        + "expanded list, and an override that drops 'params' the base has does not stop them. The modifier is inert either way, and the only "
        + "thing a mismatch does is tell a reader something untrue about the call. Make the override's modifier match the base.";
}
