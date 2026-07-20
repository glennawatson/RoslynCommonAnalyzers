// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeForwardedHeaders = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1511ForwardedHeadersTrustBoundaryRemovalAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1511 (the forwarded-headers trust boundary must not be removed).</summary>
public class ForwardedHeadersTrustBoundaryRemovalAnalyzerUnitTest
{
    /// <summary>An inline stub of the ASP.NET Core forwarded-headers options surface the rule gates on.</summary>
    private const string AspNetStubs = """

                                       namespace Microsoft.AspNetCore.Builder
                                       {
                                           public sealed class ForwardedHeadersOptions
                                           {
                                               public System.Collections.Generic.IList<int> KnownProxies { get; } = new System.Collections.Generic.List<int>();

                                               public System.Collections.Generic.IList<int> KnownNetworks { get; } = new System.Collections.Generic.List<int>();

                                               public System.Collections.Generic.IList<int> KnownIPNetworks { get; } = new System.Collections.Generic.List<int>();

                                               public int? ForwardLimit { get; set; }
                                           }
                                       }
                                       """;

    /// <summary>Verifies clearing <c>KnownProxies</c> removes the trusted-proxy restriction and is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClearKnownProxiesReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(ForwardedHeadersOptions options)
                {
                    {|SES1511:options.KnownProxies.Clear()|};
                }
            }
            """);

    /// <summary>Verifies clearing the deprecated <c>KnownNetworks</c> list is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClearKnownNetworksReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(ForwardedHeadersOptions options)
                {
                    {|SES1511:options.KnownNetworks.Clear()|};
                }
            }
            """);

    /// <summary>Verifies clearing the modern <c>KnownIPNetworks</c> list is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClearKnownIPNetworksReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(ForwardedHeadersOptions options)
                {
                    {|SES1511:options.KnownIPNetworks.Clear()|};
                }
            }
            """);

    /// <summary>Verifies a direct <c>ForwardLimit = null</c> assignment is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForwardLimitNullAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(ForwardedHeadersOptions options)
                {
                    {|SES1511:options.ForwardLimit = null|};
                }
            }
            """);

    /// <summary>Verifies a <c>ForwardLimit = null</c> object-initializer member is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForwardLimitNullInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public ForwardedHeadersOptions M()
                    => new ForwardedHeadersOptions { {|SES1511:ForwardLimit = null|} };
            }
            """);

    /// <summary>Verifies clearing the list through a <c>this</c>-qualified access on a subtype is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClearThroughLocalOptionsReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M()
                {
                    var options = new ForwardedHeadersOptions();
                    {|SES1511:options.KnownProxies.Clear()|};
                }
            }
            """);

    /// <summary>Verifies a populated trusted-proxy list (no <c>Clear</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PopulatedKnownProxiesIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(ForwardedHeadersOptions options)
                {
                    options.KnownProxies.Add(1);
                }
            }
            """);

    /// <summary>Verifies a finite <c>ForwardLimit</c> assignment is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FiniteForwardLimitIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(ForwardedHeadersOptions options)
                {
                    options.ForwardLimit = 2;
                }
            }
            """);

    /// <summary>Verifies clearing a same-named list on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClearOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class Unrelated
            {
                public System.Collections.Generic.IList<int> KnownProxies { get; } = new System.Collections.Generic.List<int>();
            }

            public class C
            {
                public void M(Unrelated other)
                {
                    other.KnownProxies.Clear();
                }
            }
            """);

    /// <summary>Verifies a <c>ForwardLimit = null</c> assignment on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForwardLimitNullOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class Unrelated
            {
                public int? ForwardLimit { get; set; }
            }

            public class C
            {
                public void M(Unrelated other)
                {
                    other.ForwardLimit = null;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the options type is not the ASP.NET Core one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenOptionsTypeUnavailableAsync()
    {
        const string Source = """
                              public sealed class ForwardedHeadersOptions
                              {
                                  public System.Collections.Generic.IList<int> KnownProxies { get; } = new System.Collections.Generic.List<int>();

                                  public int? ForwardLimit { get; set; }
                              }

                              public class C
                              {
                                  public void M(ForwardedHeadersOptions options)
                                  {
                                      options.KnownProxies.Clear();
                                      options.ForwardLimit = null;
                                  }
                              }
                              """;

        var test = new AnalyzeForwardedHeaders.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeForwardedHeaders.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + AspNetStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
