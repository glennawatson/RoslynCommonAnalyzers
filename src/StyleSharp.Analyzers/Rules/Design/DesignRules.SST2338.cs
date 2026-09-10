// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2338 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2338 — a type carries a tag plus mutually exclusive payloads, which is a union written by hand.</summary>
    public static readonly DiagnosticDescriptor PreferUnion = CreateDisabled(
        "SST2338",
        "Declare a hand-rolled discriminated type as a union",
        "'{0}' pairs a discriminator with mutually exclusive payloads; declare it as a union so the compiler enforces the invariant",
        PreferUnionDescription);

    /// <summary>The PreferUnion rule description.</summary>
    private const string PreferUnionDescription =
        "A type holding an enum tag beside several differently typed nullable payloads is a discriminated union written by "
        + "hand, and the invariant it depends on -- exactly one payload is set, and it is the one the tag names -- lives "
        + "only in whatever code remembers to check. Nothing stops a caller reading the wrong payload for the tag, or "
        + "setting two, and the failure shows up far from the mistake as a null reference. A C# 15 union states the same "
        + "shape in the type system: the value is one of the case types, conversions from each case come for free, and a "
        + "switch over the cases is checked for exhaustiveness. Reported only when the runtime actually supports unions -- "
        + "the marker interface has to resolve -- and only for the narrow shape above: exactly one enum-typed member whose "
        + "name reads as a discriminator, alongside two or more payload members of distinct nullable or reference types. "
        + "Off by default: reshaping a type into a union changes its public surface and is a migration, not a cleanup.";
}
