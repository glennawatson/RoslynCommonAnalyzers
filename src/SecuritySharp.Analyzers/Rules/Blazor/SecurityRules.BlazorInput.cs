// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1705 and SES1706 descriptors for the Blazor component input trust boundary.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1705 — a Blazor component navigates to a target that is not a verified relative URL (open redirect).</summary>
    public static readonly DiagnosticDescriptor NavigationOpenRedirect = Create(
        "SES1705",
        "A Blazor component must navigate only to a verified relative URL",
        NavigationOpenRedirectMessage,
        Blazor,
        NavigationOpenRedirectDescription);

    /// <summary>SES1706 — a Blazor uploaded-file read is given an unbounded or client-derived size limit.</summary>
    public static readonly DiagnosticDescriptor UnboundedBrowserFileRead = Create(
        "SES1706",
        "A Blazor uploaded-file read must have a bounded, server-chosen size limit",
        UnboundedBrowserFileReadMessage,
        Blazor,
        UnboundedBrowserFileReadDescription);

    /// <summary>The SES1705 rule message.</summary>
    private const string NavigationOpenRedirectMessage =
        "'{0}' navigates to a target that is not a verified relative URL; a non-constant target can carry an attacker-supplied "
        + "value and an absolute or protocol-relative target leaves the origin, so the browser can be driven to an attacker "
        + "origin (open redirect) -- pass a relative URL, or validate the target with an allow-listed validator before navigating";

    /// <summary>The SES1705 rule description.</summary>
    private const string NavigationOpenRedirectDescription =
        "'NavigationManager.NavigateTo' hands its 'uri' argument to the browser as the next location. When that argument is a "
        + "compile-time-constant relative URL (for example '/counter' or 'counter'), the navigation stays within the app's own "
        + "origin and cannot be steered. But an absolute URL ('https://attacker.example'), a protocol-relative URL "
        + "('//attacker.example'), or any non-constant value -- a query-string parameter, a field read from client state, a "
        + "value off the wire -- can send the browser to a host the attacker chose. That is an open redirect (CWE-601): a link "
        + "that appears to belong to the trusted app lands the victim on an attacker-controlled page that harvests credentials "
        + "or serves malware, and the trusted origin in the original link lends the attack credibility. The rule reports the "
        + "'uri' argument unless it is a verified relative literal (a constant, non-absolute, non-protocol-relative URL) or the "
        + "argument is produced by a validator named in 'securitysharp.SES1705.validators'. Prefer a relative URL, or pass the "
        + "target through a validator that rejects any off-origin destination before navigating. The whole rule is gated on "
        + "'Microsoft.AspNetCore.Components.NavigationManager' resolving, so a project without Blazor pays nothing.";

    /// <summary>The SES1706 rule message.</summary>
    private const string UnboundedBrowserFileReadMessage =
        "'OpenReadStream' is given {0} as its maximum allowed size; a client can then stream a file far larger than the server "
        + "expects and exhaust its memory (denial of service) -- pass a fixed, server-chosen byte limit sized to the largest "
        + "legitimate upload";

    /// <summary>The SES1706 rule description.</summary>
    private const string UnboundedBrowserFileReadDescription =
        "'IBrowserFile.OpenReadStream' caps the number of bytes it will stream at its 'maxAllowedSize' argument, which defaults "
        + "to roughly 500 KB so a caller cannot accidentally read an oversized upload into memory. Raising that cap to an "
        + "unbounded or client-controlled value hands the limit back to the attacker: 'long.MaxValue' removes the cap entirely, "
        + "and 'IBrowserFile.Size' is the length the browser reported for the file -- untrusted client metadata -- so passing it "
        + "lets the client declare its own limit. A constant above the configured threshold ('securitysharp.SES1706.max_bytes') "
        + "is reported for the same reason. With no cap, a single upload can fill the server's memory or disk and take the "
        + "process down (CWE-770, allocation of resources without limits). The no-argument 'OpenReadStream()' keeps the safe "
        + "default and is never reported. Choose a fixed limit sized to the largest legitimate upload and stream to disk rather "
        + "than buffering the whole file in memory. The whole rule is gated on "
        + "'Microsoft.AspNetCore.Components.Forms.IBrowserFile' resolving, so a project without Blazor pays nothing.";
}
