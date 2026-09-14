// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CookieOptionsSameSiteNoneWithoutSecureReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CookieBuilderSameSiteNoneWithoutSecurePolicyReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImplicitNewCookieOptionsReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SecureExplicitlyFalseReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CookieBuilderSecurePolicyNoneReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SecureSetOnLaterStatementStillReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CookieOptionsWithSecureTrueIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SecureBeforeSameSiteIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantSecureIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CookieBuilderSecurePolicyAlwaysIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameSiteLaxIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedMemberOnUnrelatedTypeIsCleanAsync() =>
        VerifyAsync(
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

        var test = new AnalyzeCookie.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies creations without a matching initializer member are ignored.</summary>
    /// <param name="creation">The creation whose initializer is absent or has no SameSite member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("new CookieOptions()")]
    [Arguments("new()")]
    [Arguments("new CookieOptions { }")]
    [Arguments("new CookieOptions { Secure = false }")]
    public Task CreationWithoutSameSiteInitializerIsCleanAsync(string creation) =>
        VerifyAsync(
            $$"""
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public CookieOptions M() => {{creation}};
            }
            """);

    /// <summary>Verifies the syntactic filter does not unwrap or evaluate SameSite expressions.</summary>
    /// <param name="value">The expression without a trailing None identifier.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("0")]
    [Arguments("(SameSiteMode)0")]
    [Arguments("(SameSiteMode.None)")]
    [Arguments("default")]
    public Task SameSiteWithoutTrailingNoneIsCleanAsync(string value) =>
        VerifyAsync(
            $$"""
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public CookieOptions M() => new CookieOptions { SameSite = {{value}} };
            }
            """);

    /// <summary>Verifies a static import of the None enum field is recognized on both cookie types.</summary>
    /// <param name="cookieType">The gated cookie type to initialize.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("CookieOptions")]
    [Arguments("CookieBuilder")]
    public Task StaticallyImportedNoneIsReportedAsync(string cookieType) =>
        VerifyAsync(
            $$"""
            using Microsoft.AspNetCore.Http;
            using static Microsoft.AspNetCore.Http.SameSiteMode;
            public class C
            {
                public {{cookieType}} M() => new() { {|SES1504:SameSite = None|} };
            }
            """);

    /// <summary>Verifies multiple candidates in one compilation keep the resolved cookie markers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultipleCookieInitializersAreReportedAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public CookieOptions[] M() => new[]
                {
                    new CookieOptions { {|SES1504:SameSite = SameSiteMode.None|} },
                    new CookieOptions { {|SES1504:SameSite = SameSiteMode.None|} },
                };
            }
            """);

    /// <summary>Verifies collection entries and indexer assignments do not pass the SameSite member filter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CollectionAndIndexerInitializersAreCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public object M() => new List<SameSiteMode> { SameSiteMode.None };
                public object N() => new Dictionary<string, SameSiteMode> { ["SameSite"] = SameSiteMode.None };
            }
            """);

    /// <summary>Verifies constrained generic creations are ignored because their created type is a type parameter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GenericCreationWithSameSitePropertyIsCleanAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;
            public interface ICookie
            {
                SameSiteMode SameSite { get; set; }
            }
            public class C
            {
                public T M<T>() where T : ICookie, new() => new T { SameSite = SameSiteMode.None };
            }
            """);

    /// <summary>Verifies a parameter named None does not bind to the enum field.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameSiteAssignedParameterNamedNoneIsCleanAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public CookieOptions M(SameSiteMode None) => new CookieOptions { SameSite = None };
            }
            """);

    /// <summary>Verifies same-named properties and foreign fields do not identify the SameSite enum field.</summary>
    /// <param name="member">The declaration that supplies a different None symbol.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("public static SameSiteMode None => SameSiteMode.None;")]
    [Arguments("public const SameSiteMode None = SameSiteMode.None;")]
    public Task SameSiteAssignedDifferentNoneSymbolIsCleanAsync(string member) =>
        VerifyAsync(
            $$"""
            using Microsoft.AspNetCore.Http;
            public class C
            {
                {{member}}
                public CookieOptions M() => new CookieOptions { SameSite = C.None };
            }
            """);

    /// <summary>Verifies an unresolved None expression is ignored while retaining the compiler error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnresolvedSameSiteValueIsCleanAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public CookieOptions M() => new CookieOptions { SameSite = {|CS0103:None|} };
            }
            """);

    /// <summary>Verifies an unresolved SameSite member is ignored while retaining the compiler error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnresolvedSameSitePropertyIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Microsoft.AspNetCore.Http
            {
                public enum SameSiteMode { None }
                public sealed class CookieOptions { }
                public class C
                {
                    public CookieOptions M() => new CookieOptions { {|CS0117:SameSite|} = SameSiteMode.None };
                }
            }
            """,
            string.Empty);

    /// <summary>Verifies a SameSite field on a cookie stub is not treated as the expected property.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameSiteFieldIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Microsoft.AspNetCore.Http
            {
                public enum SameSiteMode { None }
                public sealed class CookieOptions
                {
                    public SameSiteMode SameSite;
                }
                public class C
                {
                    public CookieOptions M() => new CookieOptions { SameSite = SameSiteMode.None };
                }
            }
            """,
            string.Empty);

    /// <summary>Verifies a missing enum field or a property named None prevents metadata gating.</summary>
    /// <param name="sameSiteDeclaration">The metadata marker with no matching field.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("public enum SameSiteMode { Lax }")]
    [Arguments("public static class SameSiteMode { public static int None => 0; }")]
    public Task MissingSameSiteNoneFieldIsCleanAsync(string sameSiteDeclaration) =>
        VerifyAsync(
            $$"""
            namespace Microsoft.AspNetCore.Http
            {
                {{sameSiteDeclaration}}
                public static class Other { public const int None = 0; }
                public sealed class CookieOptions
                {
                    public int SameSite { get; set; }
                }
                public class C
                {
                    public CookieOptions M() => new CookieOptions { SameSite = Other.None };
                    public CookieOptions N() => new CookieOptions { SameSite = Other.None };
                }
            }
            """,
            string.Empty);

    /// <summary>Verifies a resolved SameSite enum does not enable analysis without either cookie type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameSiteEnumWithoutCookieTypesIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Microsoft.AspNetCore.Http
            {
                public enum SameSiteMode { None }
            }
            public sealed class CookieOptions
            {
                public Microsoft.AspNetCore.Http.SameSiteMode SameSite { get; set; }
            }
            public class C
            {
                public CookieOptions M() => new CookieOptions
                {
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None,
                };
            }
            """,
            string.Empty);

    /// <summary>Verifies either cookie type can enable the rule independently of the other.</summary>
    /// <param name="cookieType">The only gated cookie type available in the compilation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("CookieOptions")]
    [Arguments("CookieBuilder")]
    public Task SingleAvailableCookieTypeIsReportedAsync(string cookieType) =>
        VerifyAsync(
            $$"""
            namespace Microsoft.AspNetCore.Http
            {
                public enum SameSiteMode { None }
                public sealed class {{cookieType}}
                {
                    public SameSiteMode SameSite { get; set; }
                }
                public class C
                {
                    public {{cookieType}} M() => new {{cookieType}} { {|SES1504:SameSite = SameSiteMode.None|} };
                }
            }
            """,
            string.Empty);

    /// <summary>Verifies an unrelated creation is rejected when only one cookie marker is available.</summary>
    /// <param name="cookieType">The available marker that does not match the created type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("CookieOptions")]
    [Arguments("CookieBuilder")]
    public Task UnrelatedCreationWithOneCookieMarkerIsCleanAsync(string cookieType) =>
        VerifyAsync(
            $$"""
            namespace Microsoft.AspNetCore.Http
            {
                public enum SameSiteMode { None }
                public sealed class {{cookieType}} { }
                public sealed class OtherCookie
                {
                    public SameSiteMode SameSite { get; set; }
                }
                public class C
                {
                    public OtherCookie M() => new OtherCookie { SameSite = SameSiteMode.None };
                }
            }
            """,
            string.Empty);

    /// <summary>Verifies an assigned policy is accepted when its None marker cannot be resolved.</summary>
    /// <param name="policyDeclaration">The absent or incomplete secure-policy metadata marker.</param>
    /// <param name="value">The assigned policy value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("", "0")]
    [Arguments("public enum CookieSecurePolicy { Always }", "CookieSecurePolicy.Always")]
    [Arguments("public static class CookieSecurePolicy { public static int None => 0; }", "CookieSecurePolicy.None")]
    public Task UnavailablePolicyNoneMarkerIsCleanAsync(string policyDeclaration, string value) =>
        VerifyAsync(
            $$"""
            namespace Microsoft.AspNetCore.Http
            {
                public enum SameSiteMode { None }
                {{policyDeclaration}}
                public sealed class CookieBuilder
                {
                    public SameSiteMode SameSite { get; set; }
                    public object SecurePolicy { get; set; }
                }
                public class C
                {
                    public CookieBuilder M() => new CookieBuilder { SameSite = SameSiteMode.None, SecurePolicy = {{value}} };
                }
            }
            """,
            string.Empty);

    /// <summary>Verifies non-None fields and expressions without a field symbol are accepted as securing policies.</summary>
    /// <param name="value">The policy expression that does not bind directly to CookieSecurePolicy.None.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("CookieSecurePolicy.SameAsRequest")]
    [Arguments("policy")]
    [Arguments("(CookieSecurePolicy)2")]
    [Arguments("{|CS0103:missing|}")]
    public Task PolicyWithoutNoneFieldSymbolIsCleanAsync(string value) =>
        VerifyAsync(
            $$"""
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public CookieBuilder M(CookieSecurePolicy policy)
                    => new CookieBuilder { SameSite = SameSiteMode.None, SecurePolicy = {{value}} };
            }
            """);

    /// <summary>Verifies unrelated builder members do not secure SameSite=None cookies.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedBuilderSiblingStillReportsAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public CookieBuilder M()
                    => new CookieBuilder { Name = "session", {|SES1504:SameSite = SameSiteMode.None|} };
            }
            """);

    /// <summary>Verifies indexer siblings are skipped while examining cookie security members.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IndexerSiblingDoesNotSecureCookieAsync() =>
        VerifyAsync(
            """
            namespace Microsoft.AspNetCore.Http
            {
                public enum SameSiteMode { None }
                public sealed class CookieOptions
                {
                    public SameSiteMode SameSite { get; set; }
                    public bool this[string name] { set { } }
                }
                public class C
                {
                    public CookieOptions M() => new CookieOptions
                    {
                        ["Secure"] = true,
                        {|SES1504:SameSite = SameSiteMode.None|},
                    };
                }
            }
            """,
            string.Empty);

    /// <summary>Verifies malformed non-assignment siblings are skipped while retaining their compiler error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InvalidCollectionSiblingDoesNotSecureCookieAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;
            public class C
            {
                public CookieOptions M() => new CookieOptions
                {
                    {|SES1504:SameSite = SameSiteMode.None|},
                    {|CS0747:1|},
                };
            }
            """);

    /// <summary>Verifies a constant of another type is not mistaken for an explicit false Secure value.</summary>
    /// <param name="value">The non-boolean constant assigned to the stub's Secure member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("0")]
    [Arguments("null")]
    public Task NonBooleanSecureConstantIsCleanAsync(string value) =>
        VerifyAsync(
            $$"""
            namespace Microsoft.AspNetCore.Http
            {
                public enum SameSiteMode { None }
                public sealed class CookieOptions
                {
                    public SameSiteMode SameSite { get; set; }
                    public object Secure { get; set; }
                }
                public class C
                {
                    public CookieOptions M() => new CookieOptions { SameSite = SameSiteMode.None, Secure = {{value}} };
                }
            }
            """,
            string.Empty);

    /// <summary>Runs analyzer verification with the selected cookie-type stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="stubs">The cookie metadata declarations, or an empty string when included in the source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string stubs = AspNetStubs)
    {
        var test = new AnalyzeCookie.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source + stubs };

        await test.RunAsync(CancellationToken.None);
    }
}
