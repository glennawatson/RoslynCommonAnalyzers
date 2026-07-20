// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <content>Descriptor for PSH1500 — prefer <c>TypedResults</c> over <c>Results</c> in a minimal-API handler.</content>
internal static partial class AspNetCoreRules
{
    /// <summary>PSH1500 — a minimal-API handler should return through <c>TypedResults</c>, not the untyped <c>Results</c> factory.</summary>
    public static readonly DiagnosticDescriptor PreferTypedResults = Create(
        "PSH1500",
        "Prefer TypedResults over Results in a minimal API handler",
        "Use 'TypedResults.{0}' instead of 'Results.{0}' so the handler returns a concrete, self-describing result type",
        PreferTypedResultsDescription);

    /// <summary>The PSH1500 rule description.</summary>
    private const string PreferTypedResultsDescription =
        "'Microsoft.AspNetCore.Http.Results.X' returns 'IResult', which erases the concrete response type: the framework "
        + "then infers the produced status code and body type for endpoint (OpenAPI) metadata instead of reading it off the "
        + "signature. The matching 'TypedResults.X' returns the concrete result type ('Ok<T>', 'NotFound', 'Created<T>', ...) "
        + "— strongly typed, self-describing to endpoint metadata, and free of that inference. The rule reports the "
        + "'Results.X(...)' call only where 'TypedResults' (and a matching member) exists in the referenced framework, so a "
        + "non-web project pays nothing. There is no automatic code fix: adopting the typed result can require declaring the "
        + "handler's return type (for example 'Results<Ok<T>, NotFound>' when a handler returns more than one shape), which is "
        + "not a mechanical member swap.";
}
