// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2451 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2451 — a class only its own constructors can create never creates itself.</summary>
    public static readonly DiagnosticDescriptor UncreatableClass = Create(
        "SST2451",
        "A class only its own constructors can create must actually create itself",
        "'{0}' can only be constructed from inside the type, but nothing in the type creates an instance — no code can instantiate it",
        UncreatableClassDescription);

    /// <summary>The UncreatableClass rule description.</summary>
    private const string UncreatableClassDescription =
        "Making every constructor private hands the type sole responsibility for creating its instances — a singleton, a factory "
        + "method, a set of well-known values. When no member of the type follows through with a 'new' expression, that "
        + "responsibility is never discharged: the class still compiles, its instance members still bind, and none of it can ever "
        + "run. Either add the static member that creates the instance, or open up a constructor so callers can.";
}
