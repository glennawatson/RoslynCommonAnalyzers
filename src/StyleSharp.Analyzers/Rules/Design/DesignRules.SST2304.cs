// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2304 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2304 — an event does not use the framework's handler shape.</summary>
    public static readonly DiagnosticDescriptor EventHandlerSignature = Create(
        "SST2304",
        "Events should use the standard handler signature",
        "'{0}' does not match the standard event signature (object sender, TEventArgs e)",
        EventHandlerSignatureDescription);

    /// <summary>The EventHandlerSignature rule description.</summary>
    private const string EventHandlerSignatureDescription =
        "Every tool that consumes events — the designer, the binder, the code that forwards one event to another — assumes the "
        + "'(object sender, TEventArgs e)' shape. A custom delegate works until something generic tries to handle it. Use "
        + "'EventHandler<T>' with an arguments type; it costs nothing and keeps the event usable by code you have not written yet.";
}
