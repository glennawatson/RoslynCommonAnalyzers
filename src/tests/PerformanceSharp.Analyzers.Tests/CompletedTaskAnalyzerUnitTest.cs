// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1308CompletedTaskAnalyzer,
    PerformanceSharp.Analyzers.Psh1308CompletedTaskCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1308CompletedTaskAnalyzer"/> (PSH1308 Task.CompletedTask).</summary>
public class CompletedTaskAnalyzerUnitTest
{
    /// <summary>Verifies a FromResult returned as a plain Task is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FromResultReturnedAsTaskIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M() => {|PSH1308:Task.FromResult(0)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a FromResult assigned to a plain Task variable is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FromResultAssignedToTaskIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public void M()
                                  {
                                      Task pending = {|PSH1308:Task.FromResult(true)|};
                                      pending.Wait();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public void M()
                                       {
                                           Task pending = Task.CompletedTask;
                                           pending.Wait();
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a FromResult whose value is observed stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FromResultObservedAsGenericTaskIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task<int> M() => Task.FromResult(0);
            }
            """);

    /// <summary>Verifies an awaited FromResult stays clean; the value flows to the awaiter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitedFromResultIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task<int> M() => await Task.FromResult(0);
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
