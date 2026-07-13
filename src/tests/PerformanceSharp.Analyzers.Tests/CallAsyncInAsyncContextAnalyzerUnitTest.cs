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
    /// <summary>Verifies a blocking Result read in an async method is reported and awaited instead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingResultIsAwaitedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      return {|PSH1313:LoadAsync().Result|};
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M()
                                       {
                                           return await LoadAsync();
                                       }

                                       private static Task<int> LoadAsync() => Task.FromResult(1);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a blocking Wait() in an async method is reported and awaited instead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingWaitIsAwaitedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      {|PSH1313:RunAsync().Wait()|};
                                  }

                                  private static Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await RunAsync();
                                       }

                                       private static Task RunAsync() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a blocking GetAwaiter().GetResult() chain is reported and awaited instead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingGetResultIsAwaitedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      return {|PSH1313:LoadAsync().GetAwaiter().GetResult()|};
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M()
                                       {
                                           return await LoadAsync();
                                       }

                                       private static Task<int> LoadAsync() => Task.FromResult(1);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

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

    /// <summary>Verifies the awaited replacement is parenthesized when the surrounding expression binds tighter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitIsParenthesizedWhenChainedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M()
                                  {
                                      return {|PSH1313:LoadAsync().Result|}.Length;
                                  }

                                  private static Task<string> LoadAsync() => Task.FromResult("x");
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M()
                                       {
                                           return (await LoadAsync()).Length;
                                       }

                                       private static Task<string> LoadAsync() => Task.FromResult("x");
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a blocking call in a synchronous method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockingInSyncMethodIsNotReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public int M()
                                  {
                                      return LoadAsync().Result;
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
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
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      await Task.Yield();

                                      int Local() => LoadAsync().Result;
                                      Local();
                                  }

                                  private static Task<int> LoadAsync() => Task.FromResult(1);
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
