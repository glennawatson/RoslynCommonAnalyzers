// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1507 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1507 — a declaration carries both <c>[AllowAnonymous]</c> and <c>[Authorize]</c>.</summary>
    public static readonly DiagnosticDescriptor ConflictingAnonymousAuthorization = Create(
        "SES1507",
        "AllowAnonymous and Authorize on the same declaration conflict",
        "The '[Authorize]' on this {0} has no effect: a co-located '[AllowAnonymous]' overrides it at runtime, so the {0} is not authorized",
        WebHardening,
        ConflictingAnonymousAuthorizationDescription);

    /// <summary>The SES1507 rule description.</summary>
    private const string ConflictingAnonymousAuthorizationDescription =
        "ASP.NET Core authorization resolves '[AllowAnonymous]' before '[Authorize]': when both sit on one method "
        + "or type, the anonymous marker short-circuits the pipeline and the endpoint is reachable without "
        + "authentication. The '[Authorize]' is dead code that reads as protection but enforces nothing, so an "
        + "author who added it believing the endpoint is locked down is mistaken. The rule reports only the two "
        + "markers on the same declaration -- the local, unambiguous case -- and leaves the intentional pattern of "
        + "a secured type with one opened-up member alone. Remove whichever marker does not match the intent: drop "
        + "'[AllowAnonymous]' to keep the endpoint protected, or drop '[Authorize]' to make the anonymous access "
        + "explicit.";
}
