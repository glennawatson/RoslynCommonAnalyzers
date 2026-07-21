// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2333 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2333 — a type implements a generic comparison/equality contract but not its non-generic form; opt-in.</summary>
    public static readonly DiagnosticDescriptor MissingNonGenericContract = CreateDisabled(
        "SST2333",
        "Provide the non-generic form of a generic comparison contract",
        "'{0}' implements '{1}' but not '{2}', so callers that reach for the non-generic contract silently skip it",
        MissingNonGenericContractDescription);

    /// <summary>The MissingNonGenericContract rule description.</summary>
    private const string MissingNonGenericContractDescription =
        "The runtime is full of code paths that still reach for the non-generic comparison and equality contracts — a non-generic "
        + "collection, a sort over 'IComparer', a hashtable that asks a value for 'object.Equals'. A type that implements only the "
        + "generic form ('IComparable<T>', 'IComparer<T>', 'IEqualityComparer<T>', 'IEquatable<T>') is invisible to those paths: "
        + "they fall back to reference identity or an ordering the type never meant, and the mistake never surfaces as an error. "
        + "Adding the non-generic member as a type-checked forward to the generic one closes the gap. Many modern types deliberately "
        + "omit the non-generic contract, so this is off by default and opt-in through '.editorconfig'.";
}
