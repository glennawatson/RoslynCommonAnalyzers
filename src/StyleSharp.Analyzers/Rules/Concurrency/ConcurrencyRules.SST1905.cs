// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST1905 descriptor.</summary>
internal static partial class ConcurrencyRules
{
    /// <summary>SST1905 — an <c>async void</c> method, lambda, or local function that is not an event handler.</summary>
    public static readonly DiagnosticDescriptor DoNotUseAsyncVoid = Create(
        "SST1905",
        "Do not use async void",
        "This async void {0} cannot be awaited, and its exceptions cannot be caught; return Task",
        DoNotUseAsyncVoidDescription);

    /// <summary>The DoNotUseAsyncVoid rule description.</summary>
    private const string DoNotUseAsyncVoidDescription =
        "An async void member looks awaitable and looks catchable and is neither. The caller gets control back at the first "
        + "await with no task to wait on, and any exception the body throws is raised on whatever thread resumed the method, "
        + "with no one positioned to catch it — which on the thread pool ends the process. Return 'Task' (or 'ValueTask') so a "
        + "caller can await the work and observe its failures. The one place async void is correct is a genuine event handler, "
        + "whose delegate has the '(object sender, TEventArgs e)' shape and no return value to give a Task back through; those "
        + "are left alone, and their bodies should wrap the work in try/catch because there is nowhere else for an exception "
        + "to go. A void-returning delegate that is not an event handler — 'Action', 'Action<T>' and the rest — is reported: "
        + "that is the fire-and-forget lambda that ends processes.";
}
