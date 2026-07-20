// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeCookie = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1504SameSiteNoneWithoutSecureAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1504 (a cookie initializer setting SameSite=None must mark the cookie secure).</summary>
public class SameSiteNoneWithoutSecureAnalyzerUnitTest
{
    /// <summary>Inline stubs of the ASP.NET Core cookie option types, in their real namespace so the rule resolves them.</summary>
    private const string AspNetStubs = """

                                       namespace Microsoft.AspNetCore.Http
                                       {
                                           public enum SameSiteMode
                                           {
                                               Unspecified = -1,
                                               None = 0,
                                               Lax = 1,
                                               Strict = 2
                                           }

                                           public enum CookieSecurePolicy
                                           {
                                               SameAsRequest = 0,
                                               Always = 1,
                                               None = 2
                                           }

                                           public sealed class CookieOptions
                                           {
                                               public SameSiteMode SameSite { get; set; }

                                               public bool Secure { get; set; }

                                               public bool HttpOnly { get; set; }
                                           }

                                           public sealed class CookieBuilder
                                           {
                                               public SameSiteMode SameSite { get; set; }

                                               public CookieSecurePolicy SecurePolicy { get; set; }

                                               public string Name { get; set; }
                                           }
                                       }
                                       """;

    /// <summary>Verifies a <c>CookieOptions</c> initializer with <c>SameSite = None</c> and no <c>Secure</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CookieOptionsSameSiteNoneWithoutSecureReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieOptions M()
                    => new CookieOptions { {|SES1504:SameSite = SameSiteMode.None|} };
            }
            """);

    /// <summary>Verifies a <c>CookieBuilder</c> initializer with <c>SameSite = None</c> and no <c>SecurePolicy</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CookieBuilderSameSiteNoneWithoutSecurePolicyReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieBuilder M()
                    => new CookieBuilder { {|SES1504:SameSite = SameSiteMode.None|} };
            }
            """);

    /// <summary>Verifies an implicit <c>new()</c> cookie initializer with <c>SameSite = None</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitNewCookieOptionsReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieOptions M()
                {
                    CookieOptions options = new() { {|SES1504:SameSite = SameSiteMode.None|} };
                    return options;
                }
            }
            """);

    /// <summary>Verifies an explicit <c>Secure = false</c> sibling still reports the <c>SameSite = None</c> member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecureExplicitlyFalseReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieOptions M()
                    => new CookieOptions { {|SES1504:SameSite = SameSiteMode.None|}, Secure = false };
            }
            """);

    /// <summary>Verifies a <c>CookieBuilder</c> with <c>SecurePolicy = None</c> still reports the <c>SameSite = None</c> member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CookieBuilderSecurePolicyNoneReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieBuilder M()
                    => new CookieBuilder { {|SES1504:SameSite = SameSiteMode.None|}, SecurePolicy = CookieSecurePolicy.None };
            }
            """);

    /// <summary>Verifies a <c>Secure</c> flag set on a later statement does not suppress the initializer diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecureSetOnLaterStatementStillReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieOptions M()
                {
                    var options = new CookieOptions { {|SES1504:SameSite = SameSiteMode.None|} };
                    options.Secure = true;
                    return options;
                }
            }
            """);

    /// <summary>Verifies a <c>CookieOptions</c> initializer that also sets <c>Secure = true</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CookieOptionsWithSecureTrueIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieOptions M()
                    => new CookieOptions { SameSite = SameSiteMode.None, Secure = true };
            }
            """);

    /// <summary>Verifies the <c>Secure = true</c> sibling secures the cookie regardless of member order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecureBeforeSameSiteIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieOptions M()
                    => new CookieOptions { Secure = true, SameSite = SameSiteMode.None };
            }
            """);

    /// <summary>Verifies a non-constant <c>Secure</c> value is treated as securing (no false positive).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantSecureIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieOptions M(bool isProduction)
                    => new CookieOptions { SameSite = SameSiteMode.None, Secure = isProduction };
            }
            """);

    /// <summary>Verifies a <c>CookieBuilder</c> with <c>SecurePolicy = Always</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CookieBuilderSecurePolicyAlwaysIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieBuilder M()
                    => new CookieBuilder { SameSite = SameSiteMode.None, SecurePolicy = CookieSecurePolicy.Always };
            }
            """);

    /// <summary>Verifies a <c>SameSite</c> value other than <c>None</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameSiteLaxIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public CookieOptions M()
                    => new CookieOptions { SameSite = SameSiteMode.Lax };
            }
            """);

    /// <summary>Verifies a same-named <c>SameSite</c> member on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMemberOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public sealed class MyCookie
            {
                public SameSiteMode SameSite { get; set; }
            }

            public class C
            {
                public MyCookie M()
                    => new MyCookie { SameSite = SameSiteMode.None };
            }
            """);

    /// <summary>Verifies the rule stays silent when the cookie types are absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenCookieTypesUnavailableAsync()
    {
        const string Source = """
                              public enum SameSiteMode
                              {
                                  None = 0
                              }

                              public sealed class CookieOptions
                              {
                                  public SameSiteMode SameSite { get; set; }

                                  public bool Secure { get; set; }
                              }

                              public class C
                              {
                                  public CookieOptions M()
                                      => new CookieOptions { SameSite = SameSiteMode.None };
                              }
                              """;

        var test = new AnalyzeCookie.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core cookie-type stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeCookie.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + AspNetStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
