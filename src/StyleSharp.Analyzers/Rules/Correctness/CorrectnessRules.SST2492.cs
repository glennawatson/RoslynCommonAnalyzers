// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2492 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2492 — a null guard rejects a value the signature says is allowed to be null.</summary>
    public static readonly DiagnosticDescriptor GuardOnNullableParameter = Create(
        "SST2492",
        "Do not throw for null on a parameter the signature allows to be null",
        "'{0}' {1}, yet this guard throws when it is null; the guard and the signature disagree",
        GuardOnNullableParameterDescription);

    /// <summary>The GuardOnNullableParameter rule description.</summary>
    private const string GuardOnNullableParameterDescription =
        "A parameter whose declared contract permits null — a nullable-annotated reference parameter, or an optional "
        + "parameter that defaults to null — is guarded by a throw when it is null. The signature promises callers that "
        + "null is a legal argument; the guard breaks that promise at runtime for exactly the value the signature invited. "
        + "One of the two is wrong. If null really is not allowed, tighten the signature: drop the '?' annotation or the "
        + "'= null' default so callers are warned at compile time and the guard becomes redundant. If null is allowed, "
        + "remove the throw and handle the null. No fix is offered because only the author knows which side is the mistake.";
}
