// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2460 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2460 — [DefaultValue] on a parameter, where nothing reads it.</summary>
    public static readonly DiagnosticDescriptor DefaultValueOnParameter = Create(
        "SST2460",
        "[DefaultValue] does not supply a parameter default",
        "[DefaultValue] on parameter '{0}' is a designer hint no call site reads; the value belongs in [DefaultParameterValue] or in the parameter's declaration",
        DefaultValueOnParameterDescription);

    /// <summary>The DefaultValueOnParameter rule description.</summary>
    private const string DefaultValueOnParameterDescription =
        "System.ComponentModel.DefaultValueAttribute tells designers and serializers what a property's normal value is; on a parameter "
        + "nothing reads it, and the compiler does not consult it when a call site omits the argument. Paired with [Optional] the mistake "
        + "is silent: the call compiles and the callee receives default(T) instead of the intended value. Write the default into the "
        + "parameter's declaration, or use System.Runtime.InteropServices.DefaultParameterValueAttribute for the interop shape.";
}
