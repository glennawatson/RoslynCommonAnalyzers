// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2400 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2400 — arguments are passed in an order the parameter names contradict.</summary>
    public static readonly DiagnosticDescriptor SwappedArguments = Create(
        "SST2400",
        "Arguments should be passed in the parameter's order",
        "'{0}' is passed as '{1}'; the names suggest these arguments are swapped",
        SwappedArgumentsDescription);

    /// <summary>The SwappedArguments rule description.</summary>
    private const string SwappedArgumentsDescription =
        "When the arguments at a call site have the same names as the parameters but in a different order, the call compiles and does the "
        + "wrong thing — the types line up, so nothing stops it. This is the failure a long parameter list of same-typed values invites, "
        + "and it is invisible in review. Only a genuine transposition is reported: the names must match parameters the call actually has, "
        + "in a different position.";
}
