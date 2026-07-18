// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2467 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2467 — a params overload is silently bypassed by a more specific same-arity sibling.</summary>
    public static readonly DiagnosticDescriptor BypassedParamsOverload = Create(
        "SST2467",
        "A params overload should not be silently bypassable by a more specific overload",
        "A single '{1}' argument to '{0}' binds to the overload taking '{1}', not the params array, so a call meant for the params overload silently runs a different method",
        BypassedParamsOverloadDescription);

    /// <summary>The BypassedParamsOverload rule description.</summary>
    private const string BypassedParamsOverloadDescription =
        "A method that takes a params array is a catch-all: any single trailing argument that can be stored in one array element "
        + "is accepted. When the same type also declares a same-named, same-arity overload whose last parameter is a more specific "
        + "type than the array's element type — a subclass, an implemented interface, or a value type that would otherwise box — "
        + "overload resolution prefers that overload for every call passing exactly that type, because an exact match beats the "
        + "conversion the params array's expanded form would need. A call the author wrote for the params overload silently binds to "
        + "the sibling and runs different code. The classic trap is a params 'object[]' logging or formatting method alongside an "
        + "overload taking a specific type such as 'Exception': 'Log(\"failed {0}\", ex)' quietly drops the formatting and calls the "
        + "'Exception' overload. Only a strictly more specific sibling is reported; an overload whose last parameter is the array's "
        + "own element type — the deliberate allocation-avoiding shape — does the same work and is never reported.";
}
