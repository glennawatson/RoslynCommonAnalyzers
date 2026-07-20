// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <content>Descriptor for PSH1505 — prefer a centralized exception handler over an MVC exception filter.</content>
internal static partial class AspNetCoreRules
{
    /// <summary>PSH1505 — cross-cutting error handling belongs in an exception handler, not an MVC exception filter.</summary>
    public static readonly DiagnosticDescriptor PreferExceptionHandlerOverMvcFilter = CreateInfo(
        "PSH1505",
        "Handle exceptions in an IExceptionHandler, not an MVC exception filter",
        "'{0}' implements '{1}'; move cross-cutting error handling into an IExceptionHandler that runs once in the pipeline instead of inside the MVC filter path",
        PreferExceptionHandlerOverMvcFilterDescription);

    /// <summary>The PSH1505 rule description.</summary>
    private const string PreferExceptionHandlerOverMvcFilterDescription =
        "An MVC exception filter (IExceptionFilter / IAsyncExceptionFilter) runs inside the MVC action-invocation pipeline: it only "
        + "sees exceptions raised after routing has selected an action and the heavier filter pipeline has been entered, and it is "
        + "invoked per matched action. Cross-cutting error handling belongs in an IExceptionHandler (registered with AddExceptionHandler "
        + "and UseExceptionHandler), which the middleware pipeline invokes once for any unhandled exception with less per-request "
        + "overhead. The rule fires only where IExceptionHandler exists in the referenced framework, so a project that cannot adopt it "
        + "pays nothing, and it reports only a class that names one of the filter interfaces directly in its own base list. A filter "
        + "that genuinely needs action-specific context is a deliberate choice; suppress the rule there.";
}
