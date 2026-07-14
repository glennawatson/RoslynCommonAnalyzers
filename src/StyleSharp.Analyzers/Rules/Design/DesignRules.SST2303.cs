// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2303 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2303 — an enum is marked as flags but its members are not powers of two.</summary>
    public static readonly DiagnosticDescriptor MisusedFlagsAttribute = Create(
        "SST2303",
        "Flags enums should declare bit values",
        "'{0}' is marked [Flags] but its members are not distinct bit values",
        MisusedFlagsAttributeDescription);

    /// <summary>The MisusedFlagsAttribute rule description.</summary>
    private const string MisusedFlagsAttributeDescription =
        "The [Flags] attribute tells everyone — the reader, 'ToString', 'HasFlag' — that the members combine with bitwise or. That only "
        + "works when each member owns its own bit. An enum marked [Flags] whose members run 0, 1, 2, 3 will report 'Three' as 'One, Two', "
        + "and a test for 'Three' will pass for a value that is neither. Either give the members powers of two, or drop the attribute.";
}
