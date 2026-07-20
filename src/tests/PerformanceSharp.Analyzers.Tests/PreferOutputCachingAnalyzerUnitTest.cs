// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1503PreferOutputCachingAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1503PreferOutputCachingAnalyzer"/> (PSH1503 prefer output caching over response caching).</summary>
public class PreferOutputCachingAnalyzerUnitTest
{
    /// <summary>The service-collection and application-builder surfaces the extension stubs hang off.</summary>
    private const string CoreTypesSource = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection
            {
            }
        }

        namespace Microsoft.AspNetCore.Builder
        {
            public interface IApplicationBuilder
            {
            }
        }
        """;

    /// <summary>The legacy response-caching registration surfaces, under their real SDK namespaces so the probes resolve.</summary>
    private const string ResponseCachingStubsSource = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public static class ResponseCachingServicesExtensions
            {
                public static IServiceCollection AddResponseCaching(this IServiceCollection services) => services;
            }
        }

        namespace Microsoft.AspNetCore.Builder
        {
            public static class ResponseCachingExtensions
            {
                public static IApplicationBuilder UseResponseCaching(this IApplicationBuilder app) => app;
            }
        }
        """;

    /// <summary>The output-caching surfaces, whose extensions type is the marker gating the whole rule.</summary>
    private const string OutputCachingStubsSource = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public static class OutputCacheServiceCollectionExtensions
            {
                public static IServiceCollection AddOutputCache(this IServiceCollection services) => services;
            }
        }

        namespace Microsoft.AspNetCore.Builder
        {
            public static class OutputCacheApplicationBuilderExtensions
            {
                public static IApplicationBuilder UseOutputCache(this IApplicationBuilder app) => app;
            }
        }
        """;

    /// <summary>The alternate marker: the output-caching options type on its own.</summary>
    private const string OutputCacheOptionsMarkerSource = """
        namespace Microsoft.AspNetCore.OutputCaching
        {
            public sealed class OutputCacheOptions
            {
            }
        }
        """;

    /// <summary>Verifies a qualified <c>AddResponseCaching</c> registration is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddResponseCachingIsFlaggedAsync()
        => await VerifyWithOutputCachingAsync(
            """
            using Microsoft.Extensions.DependencyInjection;

            public class C
            {
                public void M(IServiceCollection services) => {|PSH1503:services.AddResponseCaching()|};
            }
            """);

    /// <summary>Verifies a qualified <c>UseResponseCaching</c> middleware call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UseResponseCachingIsFlaggedAsync()
        => await VerifyWithOutputCachingAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app) => {|PSH1503:app.UseResponseCaching()|};
            }
            """);

    /// <summary>Verifies the options type alone gates the rule on, so the registration is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutputCacheOptionsMarkerFlagsRegistrationAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                using Microsoft.Extensions.DependencyInjection;

                public class C
                {
                    public void M(IServiceCollection services) => {|PSH1503:services.AddResponseCaching()|};
                }
                """,
        };
        test.TestState.Sources.Add(CoreTypesSource);
        test.TestState.Sources.Add(ResponseCachingStubsSource);
        test.TestState.Sources.Add(OutputCacheOptionsMarkerSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies nothing is reported when output caching is not available to adopt.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutputCachingAbsentIsCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                using Microsoft.Extensions.DependencyInjection;

                public class C
                {
                    public void M(IServiceCollection services) => services.AddResponseCaching();
                }
                """,
        };
        test.TestState.Sources.Add(CoreTypesSource);
        test.TestState.Sources.Add(ResponseCachingStubsSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a same-named method on an unrelated type is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedSameNameMethodIsCleanAsync()
        => await VerifyWithOutputCachingAsync(
            """
            public class MyServices
            {
                public void AddResponseCaching()
                {
                }
            }

            public class C
            {
                public void M(MyServices services) => services.AddResponseCaching();
            }
            """);

    /// <summary>Verifies a same-named delegate member invocation is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNameDelegateMemberIsCleanAsync()
        => await VerifyWithOutputCachingAsync(
            """
            public class Holder
            {
                public System.Action UseResponseCaching = () => { };
            }

            public class C
            {
                public void M(Holder holder) => holder.UseResponseCaching();
            }
            """);

    /// <summary>Verifies the output-caching replacements are never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlreadyUsingOutputCachingIsCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;

                public class C
                {
                    public void M(IServiceCollection services, IApplicationBuilder app)
                    {
                        services.AddOutputCache();
                        app.UseOutputCache();
                    }
                }
                """,
        };
        test.TestState.Sources.Add(CoreTypesSource);
        test.TestState.Sources.Add(OutputCachingStubsSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unqualified invocation (no member-access target) is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnqualifiedInvocationIsCleanAsync()
        => await VerifyWithOutputCachingAsync(
            """
            public class C
            {
                public void M() => Helper();

                private static void Helper()
                {
                }
            }
            """);

    /// <summary>Runs a verification with the response-caching and output-caching surfaces both present.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithOutputCachingAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.Sources.Add(CoreTypesSource);
        test.TestState.Sources.Add(ResponseCachingStubsSource);
        test.TestState.Sources.Add(OutputCachingStubsSource);

        await test.RunAsync(CancellationToken.None);
    }
}
