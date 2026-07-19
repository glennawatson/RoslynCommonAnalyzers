// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2323 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2323 — an all-abstract class states a contract an interface should carry.</summary>
    public static readonly DiagnosticDescriptor PreferInterfaceOverAbstractClass = Create(
        "SST2323",
        "Express an all-abstract class as an interface",
        "'{0}' declares only abstract members and no state; an interface expresses this contract and leaves the base type free",
        PreferInterfaceOverAbstractClassDescription);

    /// <summary>The PreferInterfaceOverAbstractClass rule description.</summary>
    private const string PreferInterfaceOverAbstractClassDescription =
        "An abstract class whose every member is abstract - no fields, no implemented member, no constructor that runs - asks its "
        + "derived types for exactly what an interface asks for, and offers none of what a base class is for. It still spends the "
        + "single base type a class is allowed, so anything deriving from it can derive from nothing else. The same members on an "
        + "interface leave that slot open, let one type satisfy several such contracts at once, and say plainly that this is a "
        + "contract and not a base. The suggestion is made only where the language can carry every one of those members on an "
        + "interface, so a project on an older version that cannot is left alone.";
}
