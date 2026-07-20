// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeKeys = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1006UnprotectedDataProtectionKeysAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1006 (persisted Data Protection keys must be encrypted at rest).</summary>
public class UnprotectedDataProtectionKeysAnalyzerUnitTest
{
    /// <summary>Inline stubs of the ASP.NET Core Data Protection builder and its persistence/protection extensions.</summary>
    private const string DataProtectionStubs = """

        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection
            {
            }
        }

        namespace Microsoft.AspNetCore.DataProtection
        {
            using Microsoft.Extensions.DependencyInjection;

            public interface IDataProtectionBuilder
            {
            }

            public sealed class DataProtectionBuilderStub : IDataProtectionBuilder
            {
            }

            public static class DataProtectionStubExtensions
            {
                public static IDataProtectionBuilder AddDataProtection(this IServiceCollection services) => new DataProtectionBuilderStub();

                public static IDataProtectionBuilder PersistKeysToFileSystem(this IDataProtectionBuilder builder, string path) => builder;

                public static IDataProtectionBuilder PersistKeysToDbContext(this IDataProtectionBuilder builder) => builder;

                public static IDataProtectionBuilder PersistKeysToAzureBlobStorage(this IDataProtectionBuilder builder, string uri) => builder;

                public static IDataProtectionBuilder PersistKeysToStackExchangeRedis(this IDataProtectionBuilder builder) => builder;

                public static IDataProtectionBuilder PersistKeysToRegistry(this IDataProtectionBuilder builder) => builder;

                public static IDataProtectionBuilder ProtectKeysWithCertificate(this IDataProtectionBuilder builder, string thumbprint) => builder;

                public static IDataProtectionBuilder ProtectKeysWithDpapi(this IDataProtectionBuilder builder) => builder;

                public static IDataProtectionBuilder ProtectKeysWithDpapiNG(this IDataProtectionBuilder builder) => builder;

                public static IDataProtectionBuilder ProtectKeysWithAzureKeyVault(this IDataProtectionBuilder builder, string keyId) => builder;
            }
        }
        """;

    /// <summary>Verifies persisting to the file system off <c>AddDataProtection()</c> with no protection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PersistToFileSystemViaAddDataProtectionReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;
            using Microsoft.Extensions.DependencyInjection;

            public class C
            {
                public void M(IServiceCollection services)
                    => services.AddDataProtection().{|SES1006:PersistKeysToFileSystem("/var/keys")|};
            }
            """);

    /// <summary>Verifies persisting to a database context with no protection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PersistToDbContextReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;

            public class C
            {
                public void M(IDataProtectionBuilder builder)
                    => builder.{|SES1006:PersistKeysToDbContext()|};
            }
            """);

    /// <summary>Verifies persisting to the registry with no protection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PersistToRegistryReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;

            public class C
            {
                public void M(IDataProtectionBuilder builder)
                    => builder.{|SES1006:PersistKeysToRegistry()|};
            }
            """);

    /// <summary>Verifies a multi-line persistence chain with no protection call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLinePersistOnlyChainReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;
            using Microsoft.Extensions.DependencyInjection;

            public class C
            {
                public void M(IServiceCollection services)
                {
                    services
                        .AddDataProtection()
                        .{|SES1006:PersistKeysToAzureBlobStorage("https://store.example/keys")|};
                }
            }
            """);

    /// <summary>Verifies a persistence call in a configuration block lambda with no protection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PersistInsideBlockLambdaWithoutProtectionReportedAsync()
        => await VerifyAsync(
            """
            using System;
            using Microsoft.AspNetCore.DataProtection;

            public class C
            {
                private static void Configure(Action<IDataProtectionBuilder> configure) => configure(null);

                public void M()
                    => Configure(builder =>
                    {
                        builder.{|SES1006:PersistKeysToStackExchangeRedis()|};
                    });
            }
            """);

    /// <summary>Verifies a null-conditional persistence call is reported (a member-binding callee).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessPersistReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;

            public class C
            {
                public void M(IDataProtectionBuilder builder)
                    => builder?.{|SES1006:PersistKeysToFileSystem("/var/keys")|};
            }
            """);

    /// <summary>Verifies the persistence extension invoked as a static call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticExtensionPersistCallReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;

            public class C
            {
                public void M(IDataProtectionBuilder builder)
                    => DataProtectionStubExtensions.{|SES1006:PersistKeysToFileSystem(builder, "/var/keys")|};
            }
            """);

    /// <summary>Verifies persistence followed by certificate protection in one chain is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PersistThenProtectSameChainIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;
            using Microsoft.Extensions.DependencyInjection;

            public class C
            {
                public void M(IServiceCollection services)
                    => services.AddDataProtection().PersistKeysToFileSystem("/var/keys").ProtectKeysWithCertificate("thumb");
            }
            """);

    /// <summary>Verifies a reversed chain (protection before persistence) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectThenPersistSameChainIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;

            public class C
            {
                public void M(IDataProtectionBuilder builder)
                    => builder.ProtectKeysWithDpapiNG().PersistKeysToFileSystem("/var/keys");
            }
            """);

    /// <summary>Verifies persistence and protection as the whole expression-lambda body is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PersistThenProtectExpressionLambdaIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using Microsoft.AspNetCore.DataProtection;

            public class C
            {
                private static void Configure(Action<IDataProtectionBuilder> configure) => configure(null);

                public void M()
                    => Configure(builder => builder.PersistKeysToFileSystem("/var/keys").ProtectKeysWithAzureKeyVault("kid"));
            }
            """);

    /// <summary>Verifies persistence and protection as separate statements of one configuration lambda are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PersistAndProtectSeparateStatementsInLambdaIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using Microsoft.AspNetCore.DataProtection;

            public class C
            {
                private static void Configure(Action<IDataProtectionBuilder> configure) => configure(null);

                public void M()
                    => Configure(builder =>
                    {
                        builder.PersistKeysToFileSystem("/var/keys");
                        builder.ProtectKeysWithDpapi();
                    });
            }
            """);

    /// <summary>Verifies a bare persistence-free Data Protection setup (and a non-member call) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoPersistenceCallIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;
            using Microsoft.Extensions.DependencyInjection;

            public class C
            {
                private static void Log()
                {
                }

                public void M(IServiceCollection services)
                {
                    Log();
                    services.AddDataProtection();
                }
            }
            """);

    /// <summary>Verifies a same-named persistence method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PersistOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.DataProtection;

            public sealed class OtherBuilder
            {
                public OtherBuilder PersistKeysToFileSystem(string path) => this;
            }

            public class C
            {
                public OtherBuilder M(OtherBuilder builder)
                    => builder.PersistKeysToFileSystem("/var/keys");
            }
            """);

    /// <summary>Verifies the rule stays silent when the Data Protection builder type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenBuilderUnavailableAsync()
    {
        const string Source = """
                              public interface IDataProtectionBuilder
                              {
                              }

                              public static class Extensions
                              {
                                  public static IDataProtectionBuilder PersistKeysToFileSystem(this IDataProtectionBuilder builder, string path) => builder;
                              }

                              public class C
                              {
                                  public void M(IDataProtectionBuilder builder)
                                      => builder.PersistKeysToFileSystem("/var/keys");
                              }
                              """;

        var test = new AnalyzeKeys.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core Data Protection stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeKeys.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + DataProtectionStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
