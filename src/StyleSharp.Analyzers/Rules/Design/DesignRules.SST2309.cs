// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2309 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2309 — an externally visible member hands a caller a default value to bake in.</summary>
    public static readonly DiagnosticDescriptor OptionalParameter = Create(
        "SST2309",
        "Use an overload instead of an optional parameter",
        "'{0}' on '{1}' is optional, so every caller that omits it compiles the default into itself",
        OptionalParameterDescription);

    /// <summary>The OptionalParameter rule description.</summary>
    private const string OptionalParameterDescription =
        "A default value is not stored in the method — it is copied into every call site that omits the argument, at the moment that "
        + "caller is compiled. Change the default in a later version and every assembly already built against the old one keeps passing the "
        + "old value, silently. An overload puts the default in one place, inside the method, where changing it reaches everybody.";
}
