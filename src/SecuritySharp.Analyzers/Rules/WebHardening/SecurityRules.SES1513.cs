// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1513 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1513 — an <c>IAuthorizationService.AuthorizeAsync</c> result is discarded instead of checked.</summary>
    public static readonly DiagnosticDescriptor DiscardedAuthorizationResult = Create(
        "SES1513",
        "An authorization result must be checked, not discarded",
        "The 'AuthorizationResult' from 'IAuthorizationService.AuthorizeAsync' is discarded; nothing reads its 'Succeeded', so execution continues whether or not authorization passed",
        WebHardening,
        DiscardedAuthorizationResultDescription);

    /// <summary>The SES1513 rule description.</summary>
    private const string DiscardedAuthorizationResultDescription =
        "'IAuthorizationService.AuthorizeAsync' does not throw when authorization fails: it returns an 'AuthorizationResult' "
        + "whose 'Succeeded' property carries the outcome. Calling it as a bare statement ('await service.AuthorizeAsync(...);') "
        + "or assigning the result to a discard ('_ = await service.AuthorizeAsync(...);') throws that outcome away, so the code "
        + "that follows runs for authorized and unauthorized principals alike -- the authorization check has no effect and the "
        + "operation it was meant to guard proceeds regardless. Capture the result and branch on it: return a forbidden or "
        + "challenge result (or throw) when 'Succeeded' is false, and only continue when it is true.";
}
