// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeLdap = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1310AnonymousLdapBindAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1310 (do not bind to a directory without authenticating).</summary>
public class AnonymousLdapBindAnalyzerUnitTest
{
    /// <summary>Minimal source-declared stubs for the directory types the rule gates on.</summary>
    private const string DirectoryServicesStubs =
        """

        namespace System.DirectoryServices
        {
            public enum AuthenticationTypes
            {
                None,
                Secure,
                Anonymous,
            }

            public sealed class DirectoryEntry
            {
                public DirectoryEntry() { }
                public DirectoryEntry(string path) { }
                public DirectoryEntry(string path, string username, string password) { }
                public DirectoryEntry(string path, string username, string password, AuthenticationTypes authenticationType) { }
                public AuthenticationTypes AuthenticationType { get; set; }
            }
        }
        """;

    /// <summary>Verifies an anonymous authentication type passed as the fourth positional argument is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnonymousAuthenticationArgumentReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new DirectoryEntry("LDAP://ad.example.com", "user", "pass", AuthenticationTypes.Anonymous)|};
                }
            }
            """);

    /// <summary>Verifies an anonymous authentication type passed by the named argument is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnonymousNamedAuthenticationArgumentReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new DirectoryEntry("LDAP://ad.example.com", "user", "pass", authenticationType: AuthenticationTypes.Anonymous)|};
                }
            }
            """);

    /// <summary>Verifies an <c>AuthenticationType = AuthenticationTypes.Anonymous</c> object-initializer member is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnonymousInitializerMemberReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new DirectoryEntry("LDAP://ad.example.com") { AuthenticationType = AuthenticationTypes.Anonymous }|};
                }
            }
            """);

    /// <summary>Verifies an LDAP bind with both credentials as empty string literals is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyStringCredentialsReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new DirectoryEntry("LDAP://ad.example.com", "", "")|};
                }
            }
            """);

    /// <summary>Verifies an LDAP bind with both credentials as <see langword="null"/> literals is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullCredentialsReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new DirectoryEntry("LDAP://ad.example.com", null, null)|};
                }
            }
            """);

    /// <summary>Verifies an LDAP bind mixing an empty string and a <see langword="null"/> credential is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedEmptyAndNullCredentialsReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new DirectoryEntry("LDAP://ad.example.com", "", null)|};
                }
            }
            """);

    /// <summary>Verifies empty credentials supplied through named arguments are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyCredentialsByNamedArgumentsReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new DirectoryEntry(path: "LDAP://ad.example.com", username: "", password: "")|};
                }
            }
            """);

    /// <summary>Verifies a lowercase <c>ldap://</c> scheme with empty credentials is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LowercaseLdapSchemeReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new DirectoryEntry("ldap://ad.example.com", "", "")|};
                }
            }
            """);

    /// <summary>Verifies a fully-qualified <c>DirectoryEntry</c> construction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedTypeReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var entry = {|SES1310:new System.DirectoryServices.DirectoryEntry("LDAP://ad.example.com", "", "")|};
                }
            }
            """);

    /// <summary>Verifies a target-typed <c>new(...)</c> empty-credential bind is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitEmptyCredentialsReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    DirectoryEntry entry = {|SES1310:new("LDAP://ad.example.com", "", "")|};
                }
            }
            """);

    /// <summary>Verifies a target-typed <c>new(...)</c> anonymous authentication type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitAnonymousAuthenticationReportedAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    DirectoryEntry entry = {|SES1310:new("LDAP://ad.example.com", "user", "pass", AuthenticationTypes.Anonymous)|};
                }
            }
            """);

    /// <summary>Verifies a bind with a real username and password is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidCredentialsAreCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry("LDAP://ad.example.com", "service", "secret");
                }
            }
            """);

    /// <summary>Verifies a path-only construction (no explicit credentials) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PathOnlyConstructionIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry("LDAP://ad.example.com");
                }
            }
            """);

    /// <summary>Verifies a parameterless construction is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessConstructionIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry();
                }
            }
            """);

    /// <summary>Verifies a non-anonymous authentication type in an object initializer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecureAuthenticationInitializerIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry { AuthenticationType = AuthenticationTypes.Secure };
                }
            }
            """);

    /// <summary>Verifies a non-anonymous authentication type argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecureAuthenticationArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry("LDAP://ad.example.com", "user", "pass", AuthenticationTypes.Secure);
                }
            }
            """);

    /// <summary>Verifies empty credentials against a non-LDAP provider path are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLdapProviderPathIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry("WinNT://ad.example.com", "", "");
                }
            }
            """);

    /// <summary>Verifies empty credentials against a path shorter than the LDAP scheme are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShortNonLdapPathIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry("id", "", "");
                }
            }
            """);

    /// <summary>Verifies an LDAP bind with only the username empty (password supplied) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OnlyUsernameEmptyIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry("LDAP://ad.example.com", "", "secret");
                }
            }
            """);

    /// <summary>Verifies non-literal credentials (a method call) against an LDAP path are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralCredentialsAreCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M()
                {
                    var entry = new DirectoryEntry("LDAP://ad.example.com", GetUser(), GetPassword());
                }

                private static string GetUser() => "service";

                private static string GetPassword() => "secret";
            }
            """);

    /// <summary>Verifies empty credentials against a non-literal path are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralPathIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M(string path)
                {
                    var entry = new DirectoryEntry(path, "", "");
                }
            }
            """);

    /// <summary>Verifies a value named <c>Anonymous</c> that is not the authentication enum member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalNamedAnonymousIsCleanAsync()
        => await VerifyAsync(
            """
            using System.DirectoryServices;

            public class C
            {
                public void M(string Anonymous)
                {
                    var entry = new DirectoryEntry("LDAP://ad.example.com", Anonymous, "secret");
                }
            }
            """);

    /// <summary>Verifies unrelated object creations (an unnamed and a generic type) are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedCreationsAreCleanAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M()
                {
                    var text = new System.Text.StringBuilder();
                    var items = new List<string>();
                }
            }
            """);

    /// <summary>Verifies a same-named <c>DirectoryEntry</c> from another namespace is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedTypeInOtherNamespaceIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var entry = new Other.DirectoryEntry("LDAP://ad.example.com", "", "");
                }
            }

            namespace Other
            {
                public sealed class DirectoryEntry
                {
                    public DirectoryEntry(string path, string username, string password) { }
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when <c>System.DirectoryServices</c> is not referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenDirectoryServicesUnavailableAsync()
    {
        const string Source = """
                              public sealed class DirectoryEntry
                              {
                                  public DirectoryEntry(string path, string username, string password) { }
                              }

                              public class C
                              {
                                  public void M()
                                  {
                                      var entry = new DirectoryEntry("LDAP://ad.example.com", "", "");
                                  }
                              }
                              """;

        var test = new AnalyzeLdap.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the directory-service stubs appended.</summary>
    /// <param name="body">The test source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string body)
    {
        var test = new AnalyzeLdap.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = body + DirectoryServicesStubs,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
