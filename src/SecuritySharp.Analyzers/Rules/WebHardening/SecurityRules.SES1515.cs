// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1515 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1515 — a Content-Security-Policy value permits inline, eval, or wildcard sources.</summary>
    public static readonly DiagnosticDescriptor PermissiveContentSecurityPolicy = DescriptorFactory.Create(
        "SES1515",
        "Restrict permissive Content-Security-Policy sources",
        PermissiveContentSecurityPolicyMessage,
        WebHardening,
        PermissiveContentSecurityPolicyDescription);

    /// <summary>The SES1515 rule message.</summary>
    private const string PermissiveContentSecurityPolicyMessage =
        "The '{0}' directive {1}";

    /// <summary>The SES1515 rule description.</summary>
    private const string PermissiveContentSecurityPolicyDescription =
        "A Content-Security-Policy can permit unrestricted inline scripts or styles, JavaScript evaluation from strings, "
        + "or resource loading from arbitrary hosts. Each source applies to its own directive, with default-src supplying "
        + "missing fetch directives. Nonces, hashes and strict-dynamic affect which sources apply in modern browsers. "
        + "Review the specific capability reported and restrict it where possible. Report-only policies do not enforce "
        + "restrictions or change another policy's enforcement.";
}
