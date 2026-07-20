// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Descriptor for PSH1502 — a web route handler returns a deferred sequence that the response
/// serializer has to enumerate synchronously on the request thread.
/// </summary>
internal static partial class AspNetCoreRules
{
    /// <summary>PSH1502 — a route handler returns a deferred <c>IEnumerable&lt;T&gt;</c> that serializes synchronously.</summary>
    public static readonly DiagnosticDescriptor LazyEnumerableRouteResult = Create(
        "PSH1502",
        "Route handler returns a deferred sequence",
        "This route handler returns a deferred sequence the response serializer enumerates synchronously on the request thread; stream it as IAsyncEnumerable<T> or materialize it before returning",
        LazyEnumerableRouteResultDescription);

    /// <summary>The PSH1502 rule description.</summary>
    private const string LazyEnumerableRouteResultDescription =
        "A route handler whose result is typed as 'IEnumerable<T>' (or 'Task<IEnumerable<T>>' / 'ValueTask<IEnumerable<T>>') but whose returned "
        + "value is a deferred query — an 'IQueryable<T>' or a lazy LINQ chain such as 'Where'/'Select'/'OrderBy' — hands the response serializer "
        + "an un-enumerated sequence. The JSON serializer then drives that enumeration synchronously on the request thread, so a database round "
        + "trip or a per-element computation runs while the thread is blocked, and under load the thread pool starves. Returning "
        + "'IAsyncEnumerable<T>' lets the framework stream the results asynchronously, and materializing the query first (for example with "
        + "'ToListAsync'/'ToArrayAsync') moves the work off the serialization path. Only the deferred shape is reported: a handler that returns an "
        + "already-materialized collection typed as 'IEnumerable<T>' is left alone. The rule costs nothing outside an ASP.NET Core project.";
}
