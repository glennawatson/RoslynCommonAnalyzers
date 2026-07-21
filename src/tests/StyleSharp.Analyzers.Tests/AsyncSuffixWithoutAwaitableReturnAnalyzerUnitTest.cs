// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAsyncMismatch = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MethodNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1321 (a synchronous method named '…Async') and its rename fix.</summary>
public class AsyncSuffixWithoutAwaitableReturnAnalyzerUnitTest
{
    /// <summary>Verifies a synchronous method with the suffix is reported and renamed to drop it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SynchronousMethodWithSuffixRenamedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int {|SST1321:FetchAsync|}() => 0;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Fetch() => 0;
                                   }
                                   """;
        await VerifyAsyncMismatch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a void method with the suffix is reported, since void is not awaitable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VoidMethodWithSuffixReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void {|SST1321:RefreshAsync|}()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Refresh()
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsyncMismatch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies awaitable return types and an async method keep the suffix without a report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitableReturnsAreCleanAsync()
        => await VerifyAsyncMismatch.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;

            public struct ValueTaskLike
            {
                public TaskAwaiter GetAwaiter() => default;
            }

            public class C
            {
                public Task SaveAsync() => Task.CompletedTask;

                public Task<int> LoadAsync() => Task.FromResult(0);

                public ValueTask StoreAsync() => default;

                public IAsyncEnumerable<int> StreamAsync() => null!;

                public ValueTaskLike ComputeAsync() => default;

                public async void OnClickAsync() => await Task.Delay(1);
            }
            """);

    /// <summary>Verifies a method named exactly 'Async' and a suffix-free synchronous method are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareSuffixAndSuffixFreeAreCleanAsync()
        => await VerifyAsyncMismatch.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Async() => 0;

                public int Fetch() => 0;
            }
            """);
}
