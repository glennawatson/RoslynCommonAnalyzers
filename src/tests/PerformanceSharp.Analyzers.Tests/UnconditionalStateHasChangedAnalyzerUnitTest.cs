// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Analyze = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1602UnconditionalStateHasChangedAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1602 (an unconditional StateHasChanged in a post-render callback).</summary>
public class UnconditionalStateHasChangedAnalyzerUnitTest
{
    /// <summary>Minimal component stubs so the marker type resolves without the framework reference.</summary>
    private const string Stubs = """

                                 namespace Microsoft.AspNetCore.Components
                                 {
                                     using System.Threading.Tasks;

                                     public abstract class ComponentBase
                                     {
                                         protected virtual void OnAfterRender(bool firstRender) { }

                                         protected virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;

                                         protected virtual void OnInitialized() { }

                                         protected void StateHasChanged() { }
                                     }
                                 }
                                 """;

    /// <summary>Verifies an unconditional StateHasChanged in the synchronous callback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnconditionalInOnAfterRenderReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  protected override void OnAfterRender(bool firstRender)
                                  {
                                      {|PSH1602:StateHasChanged()|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an unconditional StateHasChanged after an await in the async callback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnconditionalInOnAfterRenderAsyncReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  protected override async Task OnAfterRenderAsync(bool firstRender)
                                  {
                                      await Task.Yield();
                                      {|PSH1602:StateHasChanged()|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an expression-bodied unconditional StateHasChanged is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  protected override void OnAfterRender(bool firstRender) => {|PSH1602:StateHasChanged()|};
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a this-qualified unconditional StateHasChanged is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisQualifiedReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  protected override void OnAfterRender(bool firstRender)
                                  {
                                      {|PSH1602:this.StateHasChanged()|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a StateHasChanged guarded by the firstRender parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedByFirstRenderNotReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  protected override void OnAfterRender(bool firstRender)
                                  {
                                      if (firstRender)
                                      {
                                          StateHasChanged();
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a StateHasChanged guarded by a boolean state flag is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedByFlagNotReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  private bool _loaded;

                                  protected override void OnAfterRender(bool firstRender)
                                  {
                                      if (!_loaded)
                                      {
                                          _loaded = true;
                                          StateHasChanged();
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a StateHasChanged reached after an early-return firstRender guard is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EarlyReturnGuardThenUnconditionalReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  protected override void OnAfterRender(bool firstRender)
                                  {
                                      if (firstRender)
                                      {
                                          return;
                                      }

                                      {|PSH1602:StateHasChanged()|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a StateHasChanged deferred into a lambda is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeferredInLambdaNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  protected override void OnAfterRender(bool firstRender)
                                  {
                                      Action defer = () => StateHasChanged();
                                      defer();
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an unconditional StateHasChanged in a non-render lifecycle method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonRenderCallbackNotReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  protected override void OnInitialized()
                                  {
                                      StateHasChanged();
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a StateHasChanged read from a property accessor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateHasChangedInPropertyNotReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public sealed class Counter : ComponentBase
                              {
                                  public bool Ready
                                  {
                                      get
                                      {
                                          StateHasChanged();
                                          return true;
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a same-named method on a non-component type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonComponentTypeNotReportedAsync()
    {
        const string Source = """
                              public sealed class Widget
                              {
                                  private void StateHasChanged() { }

                                  public void OnAfterRender(bool firstRender)
                                  {
                                      StateHasChanged();
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies nothing is reported when the component base type is not referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBlazorReferenceNotReportedAsync()
    {
        const string Source = """
                              public abstract class ComponentBase
                              {
                                  protected virtual void OnAfterRender(bool firstRender) { }

                                  protected void StateHasChanged() { }
                              }

                              public sealed class Counter : ComponentBase
                              {
                                  protected override void OnAfterRender(bool firstRender)
                                  {
                                      StateHasChanged();
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

    /// <summary>Runs the analyzer against the source plus the component stubs on the .NET 9 reference assemblies.</summary>
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
