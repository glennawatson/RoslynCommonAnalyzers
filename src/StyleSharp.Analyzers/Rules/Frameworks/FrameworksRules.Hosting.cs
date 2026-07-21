// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2707 descriptor.</summary>
internal static partial class FrameworksRules
{
    /// <summary>SST2707 — a discarded <c>Task.Run</c> in a controller captures the request's <c>HttpContext</c>.</summary>
    public static readonly DiagnosticDescriptor FireAndForgetHttpContext = CreateDisabled(
        "SST2707",
        "Do not capture HttpContext in a fire-and-forget Task.Run",
        "This fire-and-forget 'Task.Run' captures the request's HttpContext; the context is disposed when the request ends, so the background work throws ObjectDisposedException",
        FireAndForgetHttpContextDescription);

    /// <summary>The SST2707 rule description.</summary>
    private const string FireAndForgetHttpContextDescription =
        "A controller action starts background work with 'Task.Run(...)' and discards the returned task — it is "
        + "neither awaited, returned, nor assigned — while the delegate closes over the request's 'HttpContext' "
        + "(or a value read from it, such as 'HttpContext.User' or 'HttpContext.Request'). 'HttpContext' and the "
        + "services hanging off it are scoped to the request: as soon as the response is sent the framework "
        + "disposes them and may recycle the context object for the next request. The fire-and-forget task, "
        + "however, outlives the request, so when it finally touches the captured context it reads torn state or "
        + "throws 'ObjectDisposedException' — and because nothing observes the task, the exception is swallowed "
        + "and the work silently fails. Offloading to 'Task.Run' inside a request also just moves synchronous "
        + "work onto another pool thread without freeing the request thread, so it rarely buys the throughput it "
        + "looks like it should. Copy the values you need out of 'HttpContext' into locals before starting the "
        + "work, and hand long-running work to a hosted background service or queue that has its own lifetime "
        + "instead of borrowing the request's. The rule reports only the discarded 'Task.Run' shape — an awaited, "
        + "returned, or assigned task is left alone — inside a type deriving from "
        + "'Microsoft.AspNetCore.Mvc.ControllerBase', and is off unless both that type and 'HttpContext' are "
        + "referenced. It is a heuristic and off by default; enable it in '.editorconfig' where it fits.";
}
