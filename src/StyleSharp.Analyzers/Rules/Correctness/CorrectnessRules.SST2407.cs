// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2407 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2407 — an event is declared but nothing raises it.</summary>
    public static readonly DiagnosticDescriptor EventNeverRaised = Create(
        "SST2407",
        "Declared events should be raised",
        "Nothing raises '{0}'; subscribers will wait forever",
        EventNeverRaisedDescription);

    /// <summary>The EventNeverRaised rule description.</summary>
    private const string EventNeverRaisedDescription =
        "An event nobody raises is a promise the type never keeps. Callers subscribe, the handler never runs, and there is nothing to see "
        + "at the point of failure — the bug is the absence of code. Raise it, or remove it.";
}
