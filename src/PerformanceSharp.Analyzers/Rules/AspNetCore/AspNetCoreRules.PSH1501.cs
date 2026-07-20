// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The PSH1501 descriptor.</summary>
internal static partial class AspNetCoreRules
{
    /// <summary>PSH1501 — inline middleware is written as a nested delegate instead of the two-parameter overload.</summary>
    public static readonly DiagnosticDescriptor PreferTwoParameterMiddleware = Create(
        "PSH1501",
        "Use the two-parameter middleware delegate",
        "Rewrite this nested-delegate middleware as the two-parameter 'Use((context, next) => ...)' overload to drop the per-request closure",
        PreferTwoParameterMiddlewareDescription);

    /// <summary>The PSH1501 rule description.</summary>
    private const string PreferTwoParameterMiddlewareDescription =
        "Inline middleware registered as 'app.Use(next => async context => { ...; await next(context); })' is a "
        + "'Func<RequestDelegate, RequestDelegate>': the outer delegate runs once at startup, but the inner delegate it "
        + "returns closes over 'next' and is rebuilt for the request pipeline, so the captured state is carried as an extra "
        + "closure allocation on the hot path. The two-parameter overload 'app.Use(async (context, next) => { ...; await "
        + "next(context); })' — a 'Func<HttpContext, RequestDelegate, Task>' — hands both the context and the next delegate "
        + "in directly, so there is no nested delegate to capture and no per-request closure to allocate. Only the inline "
        + "nested-lambda shape is reported: a single-parameter lambda whose body returns another lambda or anonymous method. "
        + "A single-parameter middleware that returns 'next' unchanged, a method group, or any existing delegate is left "
        + "alone, and the whole rule stays silent unless 'Microsoft.AspNetCore.Builder.IApplicationBuilder' is referenced.";
}
