// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1502 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1502 — a CORS origin predicate unconditionally allows every origin.</summary>
    public static readonly DiagnosticDescriptor AlwaysAllowedCorsOrigin = Create(
        "SES1502",
        "A CORS origin predicate must not unconditionally allow every origin",
        "The predicate passed to 'SetIsOriginAllowed' always returns true, so every origin is allowed; this is equivalent to AllowAnyOrigin and is unsafe, especially combined with credentials",
        WebHardening,
        AlwaysAllowedCorsOriginDescription);

    /// <summary>The SES1502 rule description.</summary>
    private const string AlwaysAllowedCorsOriginDescription =
        "'CorsPolicyBuilder.SetIsOriginAllowed' takes a predicate that runs per request to decide whether the request's Origin "
        + "header is allowed. A predicate that ignores its argument and always returns true -- an expression lambda '=> true', a "
        + "block whose only result is 'return true;', or a method group to such a method -- accepts every origin, which is "
        + "identical to calling 'AllowAnyOrigin' and defeats the point of an allow-list. Paired with 'AllowCredentials' it is worse: "
        + "the browser reflects the caller's origin and exposes credentialed cross-origin responses to any site, so cookies and "
        + "authenticated data leak to attacker-controlled pages. Return true only for origins you trust -- compare the origin "
        + "against an explicit allow-list -- instead of a constant true.";
}
