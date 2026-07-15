// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2429 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2429 — a write accessor never reads the value it was handed.</summary>
    public static readonly DiagnosticDescriptor AccessorIgnoresValue = Create(
        "SST2429",
        "This accessor never uses its value",
        "This '{0}' accessor never reads 'value', so the assignment is discarded",
        AccessorIgnoresValueDescription);

    /// <summary>The AccessorIgnoresValue rule description.</summary>
    private const string AccessorIgnoresValueDescription =
        "A set, init, add, or remove accessor is handed the incoming value through the contextual 'value' parameter. An accessor whose body "
        + "never mentions 'value' throws that value away: every assignment to the property or subscription to the event runs, compiles, and "
        + "does nothing the caller can observe — usually because the body reads the wrong field ('set => _height = _width;'). An accessor that "
        + "is deliberately inert is left alone: an empty body is a recognised no-op, and one that only throws is refusing the write on purpose.";
}
