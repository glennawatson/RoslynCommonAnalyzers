// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAsyncSuffix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MethodNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1317 (async method naming) and its rename fix.</summary>
public class AsyncMethodSuffixAnalyzerUnitTest
{
    /// <summary>Verifies a task-returning method without the suffix is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TaskMethodWithoutSuffixRenamedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task {|SST1317:Load|}()
                                  {
                                      await Task.Delay(1);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task LoadAsync()
                                       {
                                           await Task.Delay(1);
                                       }
                                   }
                                   """;
        await VerifyAsyncSuffix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a suffixed method, an async void handler, and a non-task method are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedCorrectlyOrNonTaskAreCleanAsync()
        => await VerifyAsyncSuffix.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task SaveAsync() => Task.CompletedTask;

                public async void OnClick()
                {
                    await Task.Delay(1);
                }

                public int Compute() => 0;
            }
            """);
}
