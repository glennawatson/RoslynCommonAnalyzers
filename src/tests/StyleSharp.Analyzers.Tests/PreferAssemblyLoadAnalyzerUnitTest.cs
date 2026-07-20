// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyLoad = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2486PreferAssemblyLoadAnalyzer>;
using VerifyLoadFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2486PreferAssemblyLoadAnalyzer,
    StyleSharp.Analyzers.Sst2486PreferAssemblyLoadCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2486 (prefer Assembly.Load over the path and partial-name load APIs).</summary>
public class PreferAssemblyLoadAnalyzerUnitTest
{
    /// <summary>Verifies a LoadFrom call is reported and not offered a fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadFromIsReportedWithoutFixAsync()
        => await VerifyReportAsync(
            """
            using System.Reflection;
            public class C
            {
                public Assembly M() => Assembly.{|SST2486:LoadFrom|}("x.dll");
            }
            """);

    /// <summary>Verifies a LoadFile call is reported and not offered a fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadFileIsReportedWithoutFixAsync()
        => await VerifyReportAsync(
            """
            using System.Reflection;
            public class C
            {
                public Assembly M() => Assembly.{|SST2486:LoadFile|}("/tmp/x.dll");
            }
            """);

    /// <summary>Verifies a LoadWithPartialName call is reported and rewritten to Assembly.Load with the same argument.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadWithPartialNameIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Reflection;
                              public class C
                              {
                                  public Assembly M() => Assembly.{|SST2486:LoadWithPartialName|}("Some.Assembly");
                              }
                              """;
        const string FixedSource = """
                                   using System.Reflection;
                                   public class C
                                   {
                                       public Assembly M() => Assembly.Load("Some.Assembly");
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix is not offered for LoadFrom, leaving the reported call unchanged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadFromIsNotRewrittenAsync()
    {
        // No fix is offered, so the reported call is unchanged and the diagnostic remains in the fixed state.
        const string Source = """
                              using System.Reflection;
                              public class C
                              {
                                  public Assembly M() => Assembly.{|SST2486:LoadFrom|}("x.dll");
                              }
                              """;
        await VerifyFixAsync(Source, Source);
    }

    /// <summary>Verifies Assembly.Load itself is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssemblyLoadIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System.Reflection;
            public class C
            {
                public Assembly M() => Assembly.Load("Some.Assembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            """);

    /// <summary>Verifies a plain, non-member-access invocation is never a candidate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainInvocationIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                private static void Helper()
                {
                }

                public void M() => Helper();
            }
            """);

    /// <summary>Verifies an instance method named LoadFrom on another type is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceMethodWithSameNameIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public void LoadFrom(string path)
                {
                }

                public void M() => this.LoadFrom("x.dll");
            }
            """);

    /// <summary>Verifies a static method named LoadFrom on a different type is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticMethodOnOtherTypeIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public static class MyLoader
            {
                public static void LoadFrom(string path)
                {
                }
            }

            public class C
            {
                public void M() => MyLoader.LoadFrom("x.dll");
            }
            """);

    /// <summary>Runs a report-and-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyLoadFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a report-only verification (analyzer, no fix) against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportAsync(string source)
    {
        var test = new VerifyLoad.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyReportAsync(source);
}
