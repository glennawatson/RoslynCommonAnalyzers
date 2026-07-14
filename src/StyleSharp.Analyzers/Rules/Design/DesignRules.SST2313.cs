// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2313 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2313 — an enum is stored as a type the project does not allow.</summary>
    public static readonly DiagnosticDescriptor EnumStorageShouldBeAllowed = Create(
        "SST2313",
        "Enums should use an allowed storage type",
        "'{0}' is stored as '{1}'; the allowed enum storage is '{2}'",
        EnumStorageShouldBeAllowedDescription);

    /// <summary>The EnumStorageShouldBeAllowed rule description.</summary>
    private const string EnumStorageShouldBeAllowedDescription =
        "An enum that names no underlying type is stored as 'int', and 'int' is what a reader, a serializer, and an interop signature all "
        + "assume unless told otherwise. Naming a different one is a real decision — 'byte' to pack a struct, 'long' to carry more than "
        + "thirty-two flags, a fixed width to match a wire format — and a decision worth making deliberately rather than by habit. The rule "
        + "does not claim to know which types a project should permit: it reports the storage types that are not on the allowed list, and the "
        + "list is yours to set with 'stylesharp.allowed_enum_storage'. It defaults to 'int' alone, which is the strict reading; a project "
        + "that packs deliberately should widen it rather than suppress the rule.";
}
