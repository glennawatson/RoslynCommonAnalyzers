// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2324 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2324 — a member is declared more accessible than its containing type can deliver.</summary>
    public static readonly DiagnosticDescriptor MemberMoreAccessibleThanContainingType = Create(
        "SST2324",
        "Do not declare a member more accessible than its containing type",
        "'{0}' is declared '{1}' but its containing type is only reachable as '{2}'",
        MemberMoreAccessibleThanContainingTypeDescription);

    /// <summary>The MemberMoreAccessibleThanContainingType rule description.</summary>
    private const string MemberMoreAccessibleThanContainingTypeDescription =
        "A member is only ever as reachable as the type that contains it. A 'public' method on an 'internal' class, "
        + "or a 'public' nested type inside one, cannot be reached from outside the assembly no matter what its own "
        + "modifier says — the container caps it. The wider modifier is then dead and misleading: a reader takes the "
        + "member for part of the public surface when nothing outside can touch it. Declare the member no wider than "
        + "its container's effective accessibility — walking up every enclosing type — so the modifier states the "
        + "reach the member actually has.";
}
