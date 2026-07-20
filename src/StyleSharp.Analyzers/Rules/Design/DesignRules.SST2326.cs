// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2326 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2326 — an interface reference is narrowed to a concrete type that implements it.</summary>
    public static readonly DiagnosticDescriptor InterfaceToConcreteCast = CreateInfo(
        "SST2326",
        "Do not narrow an interface reference to a concrete implementation type",
        "'{0}' is narrowed to the concrete type '{1}', coupling this code to one implementation of the interface",
        InterfaceToConcreteCastDescription);

    /// <summary>The InterfaceToConcreteCast rule description.</summary>
    private const string InterfaceToConcreteCastDescription =
        "A value whose static type is an interface is narrowed to a concrete class that implements it — through a cast, "
        + "an 'as', or an 'is' type test. The interface exists so callers depend on the contract and not on any one "
        + "class behind it; reaching past it to a named implementation throws that away. The code now assumes a specific "
        + "type is on the other end, so substituting another implementation — a decorator, a proxy, a test double, a "
        + "future rewrite — makes the cast fail at runtime or the type test silently take the wrong branch. Keep working "
        + "through the interface. If you need a member the interface does not expose, that member belongs on the "
        + "interface (or on a further interface the value can be typed as), not fetched by narrowing to the concrete "
        + "class. Reported as a suggestion because a deliberate, localized narrowing is sometimes the pragmatic choice.";
}
