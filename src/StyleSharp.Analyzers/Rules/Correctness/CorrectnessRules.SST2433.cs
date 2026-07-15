// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2433 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2433 — a caller-info parameter is out of place or has no default.</summary>
    public static readonly DiagnosticDescriptor CallerInfoParameterOrder = Create(
        "SST2433",
        "A caller-info parameter must come last and have a default",
        "This caller-info parameter {0}",
        CallerInfoParameterOrderDescription);

    /// <summary>The CallerInfoParameterOrder rule description.</summary>
    private const string CallerInfoParameterOrderDescription =
        "A caller-info parameter is filled in by the compiler from the call site, so callers leave it out. That only works "
        + "when the parameter comes last and has a default: a following ordinary parameter forces callers to pass the "
        + "caller-info argument positionally, and their value lands in it silently while the real parameter takes the "
        + "default; a caller-info parameter with no default forces every caller to pass it explicitly, which is the one thing "
        + "the mechanism exists to avoid. Move the caller-info parameters to the end and give each a default. Reordering an "
        + "existing signature re-maps every positional call site, so introduce a new overload rather than editing the "
        + "parameter list in place.";
}
