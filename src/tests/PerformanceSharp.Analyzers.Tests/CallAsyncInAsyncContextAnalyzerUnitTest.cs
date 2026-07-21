// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyAwait = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1313CallAsyncInAsyncContextAnalyzer,
    PerformanceSharp.Analyzers.Psh1313CallAsyncInAsyncContextCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1313 (call the async overload from an async method) and its code fix.</summary>
public class CallAsyncInAsyncContextAnalyzerUnitTest
{
    /// <summary>Verifies a synchronous call with a matching async sibling is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyncCallWithAsyncSiblingIsRewrittenAsync()
    {
        const string Source = """
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<string> M(string path)
                                  {
                                      return {|PSH1313:File.ReadAllText(path)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.IO;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<string> M(string path)
                                       {
                                           return await File.ReadAllTextAsync(path);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a blocking synchronous instance call with a matching async sibling is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyncInstanceCallWithAsyncSiblingIsRewrittenAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(SemaphoreSlim gate)
                                  {
                                      {|PSH1313:gate.Wait()|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(SemaphoreSlim gate)
                                       {
                                           await gate.WaitAsync();
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the awaited replacement is parenthesized when the surrounding expression binds tighter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitIsParenthesizedWhenChainedAsync()
    {
        const string Source = """
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(string path)
                                  {
                                      return {|PSH1313:File.ReadAllText(path)|}.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.IO;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M(string path)
                                       {
                                           return (await File.ReadAllTextAsync(path)).Length;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a blocking wait on a task belongs to PSH1315 alone, so this rule stays quiet on it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingWaitOnATaskIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Task<int> pending, CancellationToken token)
                                  {
                                      await Task.Yield();
                                      pending.Wait(token);
                                      RunAsync().GetAwaiter().GetResult();
                                      return pending.Result;
                                  }

                                  private static Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a synchronous call in a synchronous method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyncCallInSyncMethodIsNotReportedAsync()
    {
        const string Source = """
                              using System.IO;

                              public class C
                              {
                                  public string M(string path)
                                  {
                                      return File.ReadAllText(path);
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a synchronous local function inside an async method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyncLocalFunctionInsideAsyncMethodIsNotReportedAsync()
    {
        const string Source = """
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(string path)
                                  {
                                      await Task.Yield();

                                      string Local() => File.ReadAllText(path);
                                      Local();
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a same-named Async member whose signature does not match is never suggested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchedAsyncSiblingIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      await Task.Yield();
                                      return Compute(1);
                                  }

                                  private static int Compute(int value) => value;

                                  private static Task<string> ComputeAsync(string value) => Task.FromResult(value);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a synchronous call with no async sibling at all is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyncCallWithoutSiblingIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      await Task.Yield();
                                      return Compute(1);
                                  }

                                  private static int Compute(int value) => value;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a synchronous call reached through a conditional access is reported but offered no fix — rebinding the detached call would orphan its member binding.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessSyncCallReportsWithoutOfferingAFixAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task Run(SemaphoreSlim gate)
                                  {
                                      gate?{|PSH1313:.Wait()|};
                                      await Task.Yield();
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyAwait.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
