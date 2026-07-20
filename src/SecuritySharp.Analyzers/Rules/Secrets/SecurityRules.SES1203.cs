// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1203 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1203 — a database connection string authenticates with a user name but has an empty or missing password.</summary>
    public static readonly DiagnosticDescriptor EmptyConnectionStringPassword = Create(
        "SES1203",
        "Do not use an empty or missing database connection-string password",
        "This connection string names a user but supplies an empty or missing password; require a strong password or use integrated authentication",
        Secrets,
        EmptyConnectionStringPasswordDescription);

    /// <summary>The SES1203 rule description.</summary>
    private const string EmptyConnectionStringPasswordDescription =
        "A database connection string that logs in with a user name but no password lets anyone who can reach the "
        + "server authenticate as that account: the credential strength is zero. The rule reports a string literal that "
        + "is connection-string shaped -- it carries a data-source key (Server, Data Source, Host, Initial Catalog, or "
        + "Database) and a user key (User ID or Uid) -- while its password is blank, either a Password/Pwd key whose "
        + "value is empty or no password key at all. Keys are parsed case-insensitively by splitting on ';' and '='. A "
        + "connection string that uses integrated or trusted authentication (Integrated Security=true|SSPI, "
        + "Trusted_Connection=true|yes) needs no password and is not reported, and a non-empty password is a separate "
        + "concern handled elsewhere. Supply a strong password read from configuration, an environment variable, or a "
        + "secret store, or switch to integrated authentication so no password travels in the connection string.";
}
