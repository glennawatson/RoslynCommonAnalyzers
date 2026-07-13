// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1413UseUnixEpochFieldAnalyzer,
    PerformanceSharp.Analyzers.Psh1413UseUnixEpochFieldCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1413UseUnixEpochFieldAnalyzer"/> (PSH1413 read the Unix epoch from the framework).</summary>
public class UseUnixEpochFieldAnalyzerUnitTest
{
    /// <summary>Verifies the fully spelled-out UTC epoch is reported and replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UtcEpochIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public DateTime M() => {|PSH1413:new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public DateTime M() => DateTime.UnixEpoch;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the kindless epoch — the dangerous one — is reported and replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KindlessEpochIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public DateTime M() => {|PSH1413:new DateTime(1970, 1, 1)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public DateTime M() => DateTime.UnixEpoch;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the kindless epoch with an explicit zero time is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KindlessEpochWithTimeComponentsIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public DateTime M() => {|PSH1413:new DateTime(1970, 1, 1, 0, 0, 0)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public DateTime M() => DateTime.UnixEpoch;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the offset epoch is reported and replaced with its own field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OffsetEpochIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public DateTimeOffset M() => {|PSH1413:new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public DateTimeOffset M() => DateTimeOffset.UnixEpoch;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies constant components are matched as the literals are.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantComponentsAreFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private const int EpochYear = 1970;

                                  public DateTime M() => {|PSH1413:new DateTime(EpochYear, 1, 1)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private const int EpochYear = 1970;

                                       public DateTime M() => DateTime.UnixEpoch;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a fully qualified allocation keeps the qualification the author wrote.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedEpochKeepsQualificationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.DateTime M() => {|PSH1413:new System.DateTime(1970, 1, 1)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.DateTime M() => System.DateTime.UnixEpoch;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an explicitly local epoch is not reported: it names a different instant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalKindEpochIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public DateTime M() => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
            }
            """);

    /// <summary>Verifies an explicitly unspecified epoch is not reported: written out, it is a choice.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnspecifiedKindEpochIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public DateTime M() => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            }
            """);

    /// <summary>Verifies another date is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherDateIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public DateTime M() => new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            """);

    /// <summary>Verifies a non-zero offset is not reported: it is not the epoch.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonZeroOffsetIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public DateTimeOffset M() => new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.FromHours(1));
            }
            """);

    /// <summary>Verifies a non-zero time component is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonZeroTimeIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public DateTime M() => new DateTime(1970, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            }
            """);

    /// <summary>Verifies the rule is silent where the epoch field does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EpochIsCleanWithoutFieldAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public DateTime M() => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                              }
                              """;

        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
            FixedCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyAsync(source, source);
}
