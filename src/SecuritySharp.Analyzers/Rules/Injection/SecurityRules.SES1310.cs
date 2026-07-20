// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1310 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1310 — a directory <c>DirectoryEntry</c> binds anonymously or without credentials.</summary>
    public static readonly DiagnosticDescriptor AnonymousLdapBind = Create(
        "SES1310",
        "Do not bind to a directory without authenticating",
        AnonymousLdapBindMessage,
        Injection,
        AnonymousLdapBindDescription);

    /// <summary>The SES1310 rule message.</summary>
    private const string AnonymousLdapBindMessage =
        "This 'DirectoryEntry' performs an anonymous directory bind because {0}; an unauthenticated bind lets an "
        + "attacker read the directory, or have the application trust attacker-controlled directory data, without "
        + "proving identity -- supply a real user and password and a non-anonymous authentication type";

    /// <summary>The SES1310 rule description.</summary>
    private const string AnonymousLdapBindDescription =
        "A 'System.DirectoryServices.DirectoryEntry' that binds anonymously -- either by passing "
        + "'AuthenticationTypes.Anonymous' (as the 'authenticationType' constructor argument or the 'AuthenticationType' "
        + "object-initializer member) or by binding to an 'LDAP://' path with an explicitly empty username and password -- "
        + "authenticates as nobody. An anonymous bind lets any unauthenticated party enumerate the directory that the bind "
        + "exposes, and, because the connection carries no proven identity, any authorization the application layers on top of "
        + "that connection is trusting data it never verified. Bind with a real service account and password and a non-anonymous "
        + "authentication type (for example 'AuthenticationTypes.Secure'), so the directory proves who it is talking to before "
        + "it answers. The rule reports only the explicit anonymous shapes and stays silent when credentials are supplied, when "
        + "the authentication type is anything other than 'Anonymous', or when 'System.DirectoryServices' is not referenced.";
}
