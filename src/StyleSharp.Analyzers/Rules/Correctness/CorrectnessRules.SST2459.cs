// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2459 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2459 — [Optional] written on a parameter that callers can never omit.</summary>
    public static readonly DiagnosticDescriptor OptionalByRefParameter = Create(
        "SST2459",
        "[Optional] cannot make a ref or out parameter optional",
        "'{0}' is a '{1}' parameter, so [Optional] does not let callers omit it; the advertised optionality does not exist",
        OptionalByRefParameterDescription);

    /// <summary>The OptionalByRefParameter rule description.</summary>
    private const string OptionalByRefParameterDescription =
        "A ref or out argument must name a variable at every call site, and [Optional] does not change that: a call that omits the "
        + "argument does not compile. The attribute is not inert, though — it flips the parameter's reflection-visible IsOptional flag, "
        + "so late-bound and reflection-based callers are told they may omit an argument that every compiled call must supply. If the "
        + "parameter should be omittable, pass it by value or by 'in', or add an overload without it. Members of COM-imported types are "
        + "not reported: there the compiler really does let callers omit an [Optional] ref argument.";
}
