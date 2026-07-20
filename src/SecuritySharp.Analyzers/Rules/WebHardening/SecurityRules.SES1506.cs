// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1506 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1506 — the developer exception page is enabled without a development-environment guard.</summary>
    public static readonly DiagnosticDescriptor UnguardedDeveloperExceptionPage = new(
        "SES1506",
        "The developer exception page must be guarded by a development-environment check",
        "'UseDeveloperExceptionPage' is enabled without a development-environment guard; in production it renders full exception detail and stack traces to the client and leaks sensitive internals",
        WebHardening,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: UnguardedDeveloperExceptionPageDescription,
        helpLinkUri: "https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/SES1506.md");

    /// <summary>The SES1506 rule description.</summary>
    private const string UnguardedDeveloperExceptionPageDescription =
        "'UseDeveloperExceptionPage' installs middleware that catches an unhandled request exception and writes the full "
        + "message, stack trace, and request state straight back to the client. That detail is a debugging aid for a "
        + "developer, and a reconnaissance gift for an attacker: in production it discloses file paths, type and framework "
        + "versions, connection strings surfaced in messages, and the internal call graph. The middleware is meant to run "
        + "only in Development, so the call belongs behind a development-environment guard -- an enclosing 'if' (or "
        + "conditional) whose condition calls 'IsDevelopment' (for example 'app.Environment.IsDevelopment()'). This rule "
        + "reports an 'UseDeveloperExceptionPage' invocation on 'Microsoft.AspNetCore.Builder.DeveloperExceptionPageExtensions' "
        + "that no such guard lexically encloses. The guard scan is purely local -- no data-flow -- so keep the check "
        + "directly around the call. Wrap the call in a development guard, or serve a hardened error handler "
        + "('UseExceptionHandler') in production instead.";
}
