// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2425 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2425 — an override forwards to the base but drops one of its own optional arguments.</summary>
    public static readonly DiagnosticDescriptor OverrideDropsOptionalArgument = Create(
        "SST2425",
        "Forward the optional argument to the base call",
        "'{0}' is not forwarded to the base call, so the base substitutes its own default and the value this method received for '{0}' is discarded",
        OverrideDropsOptionalArgumentDescription);

    /// <summary>The OverrideDropsOptionalArgument rule description.</summary>
    private const string OverrideDropsOptionalArgumentDescription =
        "An override takes an optional parameter from its caller and then calls the method it overrides without passing that parameter on. "
        + "The base call fills in the base's own default, so whatever value the caller supplied is silently thrown away before the base ever "
        + "sees it. This is easy to write when the forwarding call is short and the omission is invisible. Pass the parameter through to the "
        + "base call so the caller's value reaches it.";
}
