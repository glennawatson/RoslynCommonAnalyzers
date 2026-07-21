// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Analyze = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1601JsInteropInLoopAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1601 (a JavaScript-interop call issued once per loop iteration).</summary>
public class JsInteropInLoopAnalyzerUnitTest
{
    /// <summary>Minimal JavaScript-interop stubs so the marker types resolve without the framework reference.</summary>
    private const string Stubs = """

                                 namespace Microsoft.JSInterop
                                 {
                                     using System.Threading.Tasks;

                                     public interface IJSRuntime
                                     {
                                         ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object[] args);

                                         ValueTask InvokeVoidAsync(string identifier, params object[] args);
                                     }

                                     public interface IJSObjectReference
                                     {
                                         ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object[] args);

                                         ValueTask InvokeVoidAsync(string identifier, params object[] args);
                                     }
                                 }
                                 """;

    /// <summary>Verifies a void interop call inside a foreach is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEachInvokeVoidAsyncReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.JSInterop;

                              public sealed class Widget
                              {
                                  public void Highlight(IJSRuntime js, List<int> items)
                                  {
                                      foreach (var item in items)
                                      {
                                          {|PSH1601:js.InvokeVoidAsync("highlight", item)|};
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a value-returning interop call inside a for loop is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForInvokeAsyncReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.JSInterop;

                              public sealed class Widget
                              {
                                  public void Read(IJSRuntime js, List<int> items)
                                  {
                                      for (int i = 0; i < items.Count; i++)
                                      {
                                          {|PSH1601:js.InvokeAsync<int>("get", items[i])|};
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an interop call on a JavaScript object reference inside a loop is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectReferenceInvokeVoidAsyncReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.JSInterop;

                              public sealed class Widget
                              {
                                  public void Highlight(IJSObjectReference module, List<int> items)
                                  {
                                      foreach (var item in items)
                                      {
                                          {|PSH1601:module.InvokeVoidAsync("highlight", item)|};
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an interop call on a concrete runtime implementation inside a loop is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteRuntimeImplementationReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Threading.Tasks;
                              using Microsoft.JSInterop;

                              public sealed class HostRuntime : IJSRuntime
                              {
                                  public ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object[] args) => default;

                                  public ValueTask InvokeVoidAsync(string identifier, params object[] args) => default;
                              }

                              public sealed class Widget
                              {
                                  public void Highlight(HostRuntime js, List<int> items)
                                  {
                                      foreach (var item in items)
                                      {
                                          {|PSH1601:js.InvokeVoidAsync("highlight", item)|};
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an interop call deferred into a lambda inside the loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InteropInsideLambdaNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;
                              using Microsoft.JSInterop;

                              public sealed class Widget
                              {
                                  public void Highlight(IJSRuntime js, List<int> items)
                                  {
                                      foreach (var item in items)
                                      {
                                          Action defer = () => js.InvokeVoidAsync("highlight", item);
                                          defer();
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a single interop call outside any loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InteropOutsideLoopNotReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public sealed class Widget
                              {
                                  public void Highlight(IJSRuntime js, int item)
                                  {
                                      js.InvokeVoidAsync("highlight", item);
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a same-named call on a non-interop receiver inside a loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonInteropReceiverNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Threading.Tasks;

                              public sealed class Bridge
                              {
                                  public ValueTask InvokeVoidAsync(string identifier, params object[] args) => default;
                              }

                              public sealed class Widget
                              {
                                  public void Highlight(Bridge bridge, List<int> items)
                                  {
                                      foreach (var item in items)
                                      {
                                          bridge.InvokeVoidAsync("highlight", item);
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a non-interop receiver in a loop is not reported when the framework has no object-reference type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeOnlyFrameworkNonInteropReceiverNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Threading.Tasks;

                              public sealed class Bridge
                              {
                                  public ValueTask InvokeVoidAsync(string identifier, params object[] args) => default;
                              }

                              public sealed class Widget
                              {
                                  public void Highlight(Bridge bridge, List<int> items)
                                  {
                                      foreach (var item in items)
                                      {
                                          bridge.InvokeVoidAsync("highlight", item);
                                      }
                                  }
                              }
                              """;
        const string RuntimeOnlyStubs = """

                                        namespace Microsoft.JSInterop
                                        {
                                            using System.Threading.Tasks;

                                            public interface IJSRuntime
                                            {
                                                ValueTask InvokeVoidAsync(string identifier, params object[] args);
                                            }
                                        }
                                        """;
        var test = new Analyze.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source + "\n" + RuntimeOnlyStubs,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies nothing is reported when the JavaScript-runtime type is not referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBlazorReferenceNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Threading.Tasks;

                              public interface IJSRuntime
                              {
                                  ValueTask InvokeVoidAsync(string identifier, params object[] args);
                              }

                              public sealed class Widget
                              {
                                  public void Highlight(IJSRuntime js, List<int> items)
                                  {
                                      foreach (var item in items)
                                      {
                                          js.InvokeVoidAsync("highlight", item);
                                      }
                                  }
                              }
                              """;
        var test = new Analyze.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the interop stubs on the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Analyze.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + "\n" + Stubs,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
