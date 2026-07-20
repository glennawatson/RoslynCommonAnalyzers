// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1512 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1512 — a sensitive framework diagnostic switch is enabled outside a development-environment guard.</summary>
    public static readonly DiagnosticDescriptor SensitiveFrameworkDiagnosticsEnabled = Create(
        "SES1512",
        "Sensitive framework diagnostics must be guarded by a development-environment check",
        "'{0}' enables sensitive framework logging (parameter values, personally identifiable information, or full security tokens) without a development-environment guard; confine it to development",
        WebHardening,
        SensitiveFrameworkDiagnosticsEnabledDescription);

    /// <summary>The SES1512 rule description.</summary>
    private const string SensitiveFrameworkDiagnosticsEnabledDescription =
        "Several framework switches deliberately widen diagnostic output to include values that are normally withheld: "
        + "'DbContextOptionsBuilder.EnableSensitiveDataLogging()' writes the concrete parameter values bound into every SQL "
        + "command into the logs; 'IdentityModelEventSource.ShowPII = true' un-redacts personally identifiable information in "
        + "identity/token logs; and 'IdentityModelEventSource.LogCompleteSecurityArtifact = true' logs the full security token, "
        + "including its signature and any embedded claims. In production those logs capture credentials, tokens, and PII and "
        + "hand them to anyone with log access (CWE-215, CWE-532). Each switch is a debugging aid meant only for Development, so "
        + "it belongs behind a development-environment guard -- an enclosing 'if' (or conditional) whose condition calls "
        + "'IsDevelopment' (for example 'app.Environment.IsDevelopment()'). This rule reports the enabling call or assignment "
        + "when no such guard lexically encloses it. The guard scan is purely local -- no data-flow -- so keep the check directly "
        + "around the switch, and remove it (or guard it) before shipping.";
}
