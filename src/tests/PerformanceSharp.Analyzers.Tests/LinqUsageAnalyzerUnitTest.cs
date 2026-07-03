// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyLinqUsage = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.LinqUsageAnalyzer,
    PerformanceSharp.Analyzers.LinqUsageCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for the LINQ usage rules (PSH1100, PSH1101, PSH1102) and their fixes.</summary>
public class LinqUsageAnalyzerUnitTest
{
    /// <summary>Verifies a Where predicate is carried by the terminal LINQ call (PSH1101).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereAnyChainIsCollapsedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public bool M(int[] values) => values.Where(value => value > 0).{|PSH1101:Any|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public bool M(int[] values) => values.Any(value => value > 0);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a Where predicate moves into a terminal Count call (PSH1101).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereCountChainIsCollapsedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public int M(int[] values) => values.Where(value => value > 0).{|PSH1101:Count|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public int M(int[] values) => values.Count(value => value > 0);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a LINQ type check followed by Cast is represented as one typed filter (PSH1102).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereTypeCheckCastChainUsesOneTypedFilterAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(object[] values) => values.Where(value => value is string).{|PSH1102:Cast<string>|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public object M(object[] values) => values.OfType<string>();
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a Cast to a different type than the checked one is not collapsed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereTypeCheckWithDifferentCastTypeIsNotReportedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(object[] values) => values.Where(value => value is string).Cast<object>();
                              }
                              """;
        var test = CreateNet80Test(Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies hot-path projects can opt into LINQ method diagnostics (PSH1100).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HotPathLinqCallIsReportedWhenEnabledAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.{|PSH1100:Select|}(value => value + 1);
                              }
                              """;
        var test = CreateNet80Test(Source);
        EnableHotPathRule(test);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the hot-path rule stays silent without the opt-in editorconfig key.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HotPathLinqCallIsNotReportedByDefaultAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.Select(value => value + 1);
                              }
                              """;
        var test = CreateNet80Test(Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies string instance methods with LINQ operator names are not reported as hot-path LINQ calls.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StringContainsIsNotHotPathLinqAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public bool M()
                                  {
                                      string name = "abc";
                                      var doesContain = name.Contains("ab");
                                      return doesContain;
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source);
        EnableHotPathRule(test);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a .NET 8 verifier test.</summary>
    /// <param name="source">The source.</param>
    /// <param name="fixedSource">The optional fixed source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyLinqUsage.Test CreateNet80Test(string source, string? fixedSource = null)
    {
        var test = new VerifyLinqUsage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        return test;
    }

    /// <summary>Enables the opt-in hot-path LINQ rule (PSH1100) for a verifier test.</summary>
    /// <param name="test">The verifier test.</param>
    private static void EnableHotPathRule(VerifyLinqUsage.Test test)
    {
        const string Config = """
                              root = true

                              [*.cs]
                              dotnet_diagnostic.PSH1100.severity = warning
                              performancesharp.avoid_linq_on_hot_path = true
                              """;
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
    }
}
