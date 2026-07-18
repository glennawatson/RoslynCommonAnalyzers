// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2304 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2304 — an event does not use the framework's handler delegate.</summary>
    public static readonly DiagnosticDescriptor EventHandlerSignature = Create(
        "SST2304",
        "Events should use the standard handler signature",
        "'{0}' should use '{1}' instead of the custom delegate '{2}'",
        EventHandlerSignatureDescription);

    /// <summary>The EventHandlerSignature rule description.</summary>
    private const string EventHandlerSignatureDescription =
        "Every tool that consumes events — the designer, the binder, the code that forwards one event to another — assumes the "
        + "'(object sender, TEventArgs e)' shape published through 'EventHandler' or 'EventHandler<TEventArgs>'. A custom delegate "
        + "of another shape cannot be handled generically at all, and even one that matches the shape blocks handler reuse and "
        + "adds API surface the framework delegate already provides. Use 'EventHandler<T>' with an arguments type; it costs "
        + "nothing and keeps the event usable by code you have not written yet.";
}
