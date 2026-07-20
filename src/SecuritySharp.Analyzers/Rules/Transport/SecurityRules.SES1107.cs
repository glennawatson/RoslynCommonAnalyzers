// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1107 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1107 — a SQL connection is configured to bypass certificate trust or transport encryption.</summary>
    public static readonly DiagnosticDescriptor WeakenedSqlTransportSecurity = Create(
        "SES1107",
        "Do not weaken SQL connection transport security",
        "The SQL connection is configured with '{0}', which bypasses server-certificate validation or transport encryption and exposes the connection to interception or tampering",
        Transport,
        WeakenedSqlTransportSecurityDescription);

    /// <summary>The SES1107 rule description.</summary>
    private const string WeakenedSqlTransportSecurityDescription =
        "A SQL connection reaches the database server over the network. Setting 'TrustServerCertificate=true' makes the client "
        + "accept any certificate the server presents without validating its chain or name, so an attacker who can intercept the "
        + "connection can present a forged certificate and read or alter every query and result -- a man-in-the-middle attack. "
        + "Setting 'Encrypt=false' (or 'Encrypt=Optional' on the newer client) turns TLS off or makes it negotiable, so the "
        + "connection -- including credentials and data -- can travel in cleartext. The rule reports a compile-time SQL "
        + "connection configuration in local, high-precision shapes: a literal connection string carrying one of these keyword "
        + "settings that is passed to a 'SqlConnection' or 'SqlConnectionStringBuilder' constructor or assigned to a "
        + "'ConnectionString' property, and a 'SqlConnectionStringBuilder' object initializer or property assignment that sets "
        + "'TrustServerCertificate = true', 'Encrypt = false', or 'Encrypt = SqlConnectionEncryptOption.Optional'. Leave the "
        + "server certificate validated and keep encryption on: install a trusted certificate on the server rather than "
        + "disabling validation. No automatic fix is offered because removing these settings can break a connection to a server "
        + "that is not yet configured for TLS, a decision only the author can make.";
}
