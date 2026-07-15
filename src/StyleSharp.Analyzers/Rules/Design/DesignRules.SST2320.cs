// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2320 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2320 — an interface inherits the same member from two unrelated base interfaces.</summary>
    public static readonly DiagnosticDescriptor AmbiguousInheritedInterfaceMember = Create(
        "SST2320",
        "An interface should not inherit the same member from two interfaces",
        "'{0}' inherits a member named '{1}' from both '{2}' and '{3}'; callers get an ambiguity error — re-declare it here with 'new' to resolve it",
        AmbiguousInheritedInterfaceMemberDescription);

    /// <summary>The AmbiguousInheritedInterfaceMember rule description.</summary>
    private const string AmbiguousInheritedInterfaceMemberDescription =
        "An interface derives from two base interfaces that each declare a member of the same name and signature, and the two are separate "
        + "declarations rather than one member reached through a shared base. The interface itself compiles, but every consumer that accesses "
        + "the member through it gets a hard compiler ambiguity error, because the compiler cannot choose between the two. Move that failure "
        + "to the author: re-declare the member on the derived interface with 'new' to pick one and silence the ambiguity for every caller.";
}
