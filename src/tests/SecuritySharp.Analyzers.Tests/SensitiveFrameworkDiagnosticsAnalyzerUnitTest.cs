// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeSensitiveDiagnostics = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1512SensitiveFrameworkDiagnosticsAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1512 (sensitive framework diagnostics enabled without a development guard).</summary>
public class SensitiveFrameworkDiagnosticsAnalyzerUnitTest
{
    /// <summary>Inline stubs of the EF Core option builders, the identity event source, and a development probe.</summary>
    private const string FrameworkStubs = """

                                          namespace Microsoft.EntityFrameworkCore
                                          {
                                              public class DbContext
                                              {
                                              }

                                              public class DbContextOptionsBuilder
                                              {
                                                  public virtual DbContextOptionsBuilder EnableSensitiveDataLogging(bool sensitiveDataLoggingEnabled = true) => this;

                                                  public virtual DbContextOptionsBuilder UseInMemoryDatabase() => this;
                                              }

                                              public class DbContextOptionsBuilder<TContext> : DbContextOptionsBuilder
                                                  where TContext : DbContext
                                              {
                                                  public new virtual DbContextOptionsBuilder<TContext> EnableSensitiveDataLogging(bool sensitiveDataLoggingEnabled = true) => this;
                                              }
                                          }

                                          namespace Microsoft.IdentityModel.Logging
                                          {
                                              public class IdentityModelEventSource
                                              {
                                                  public static bool ShowPII { get; set; }

                                                  public static bool LogCompleteSecurityArtifact { get; set; }
                                              }
                                          }

                                          public sealed class HostEnvironment
                                          {
                                              public HostEnvironment Environment => this;

                                              public bool IsDevelopment() => true;
                                          }

                                          public sealed class AppContext : Microsoft.EntityFrameworkCore.DbContext
                                          {
                                          }
                                          """;

    /// <summary>Verifies a bare <c>EnableSensitiveDataLogging()</c> on the non-generic builder is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnableSensitiveDataLoggingReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;

            public class C
            {
                public void M(DbContextOptionsBuilder builder)
                {
                    {|SES1512:builder.EnableSensitiveDataLogging()|};
                }
            }
            """);

    /// <summary>Verifies <c>EnableSensitiveDataLogging()</c> on the generic builder is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericBuilderEnableSensitiveDataLoggingReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;

            public class C
            {
                public void M(DbContextOptionsBuilder<AppContext> builder)
                {
                    {|SES1512:builder.EnableSensitiveDataLogging()|};
                }
            }
            """);

    /// <summary>Verifies an explicit <c>EnableSensitiveDataLogging(true)</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnableSensitiveDataLoggingExplicitTrueReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;

            public class C
            {
                public void M(DbContextOptionsBuilder builder)
                {
                    {|SES1512:builder.EnableSensitiveDataLogging(true)|};
                }
            }
            """);

    /// <summary>Verifies <c>IdentityModelEventSource.ShowPII = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShowPiiAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Logging;

            public class C
            {
                public void M()
                {
                    {|SES1512:IdentityModelEventSource.ShowPII = true|};
                }
            }
            """);

    /// <summary>Verifies <c>IdentityModelEventSource.LogCompleteSecurityArtifact = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LogCompleteSecurityArtifactAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Logging;

            public class C
            {
                public void M()
                {
                    {|SES1512:IdentityModelEventSource.LogCompleteSecurityArtifact = true|};
                }
            }
            """);

    /// <summary>Verifies an <c>EnableSensitiveDataLogging</c> call guarded by <c>IsDevelopment</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DevelopmentGuardedEnableIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;

            public class C
            {
                public void M(DbContextOptionsBuilder builder, HostEnvironment env)
                {
                    if (env.IsDevelopment())
                    {
                        builder.EnableSensitiveDataLogging();
                    }
                }
            }
            """);

    /// <summary>Verifies a <c>ShowPII</c> assignment behind a chained <c>Environment.IsDevelopment()</c> guard is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DevelopmentGuardedShowPiiIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Logging;

            public class C
            {
                public void M(HostEnvironment builder)
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        IdentityModelEventSource.ShowPII = true;
                    }
                }
            }
            """);

    /// <summary>Verifies an explicit <c>EnableSensitiveDataLogging(false)</c> disabling call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnableSensitiveDataLoggingFalseIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;

            public class C
            {
                public void M(DbContextOptionsBuilder builder)
                {
                    builder.EnableSensitiveDataLogging(false);
                }
            }
            """);

    /// <summary>Verifies an inline environment-flag argument is treated as the developer's own gate and not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnableSensitiveDataLoggingWithRuntimeFlagIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;

            public class C
            {
                public void M(DbContextOptionsBuilder builder, HostEnvironment env)
                {
                    builder.EnableSensitiveDataLogging(env.IsDevelopment());
                }
            }
            """);

    /// <summary>Verifies assigning <c>false</c> to <c>ShowPII</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShowPiiAssignedFalseIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Logging;

            public class C
            {
                public void M()
                {
                    IdentityModelEventSource.ShowPII = false;
                }
            }
            """);

    /// <summary>Verifies a same-named <c>EnableSensitiveDataLogging</c> on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMethodOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public sealed class MyBuilder
            {
                public MyBuilder EnableSensitiveDataLogging() => this;
            }

            public class C
            {
                public void M(MyBuilder builder)
                {
                    builder.EnableSensitiveDataLogging();
                }
            }
            """);

    /// <summary>Verifies a same-named static <c>ShowPII</c> on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedPropertyOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public static class MyLogging
            {
                public static bool ShowPII { get; set; }
            }

            public class C
            {
                public void M()
                {
                    MyLogging.ShowPII = true;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when neither framework type is present in the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenFrameworkTypesUnavailableAsync()
    {
        const string Source = """
                              public sealed class DbContextOptionsBuilder
                              {
                                  public DbContextOptionsBuilder EnableSensitiveDataLogging() => this;
                              }

                              public static class IdentityModelEventSource
                              {
                                  public static bool ShowPII { get; set; }
                              }

                              public class C
                              {
                                  public void M(DbContextOptionsBuilder builder)
                                  {
                                      builder.EnableSensitiveDataLogging();
                                      IdentityModelEventSource.ShowPII = true;
                                  }
                              }
                              """;

        var test = new AnalyzeSensitiveDiagnostics.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline framework-type stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeSensitiveDiagnostics.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + FrameworkStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
