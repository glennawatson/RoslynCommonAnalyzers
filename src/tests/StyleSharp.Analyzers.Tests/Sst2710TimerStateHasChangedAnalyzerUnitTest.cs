// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyTimer = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2710TimerStateHasChangedAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2710TimerStateHasChangedAnalyzer"/> (SST2710), which reports
/// <c>StateHasChanged</c> called directly from a timer callback without marshalling onto the dispatcher.
/// </summary>
public class Sst2710TimerStateHasChangedAnalyzerUnitTest
{
    /// <summary>
    /// An in-source stub of the component base type, added as a second document so the marker resolves without a
    /// package restore. Omitting it is what the "not referenced" gate test relies on.
    /// </summary>
    private const string ComponentsStub = """
                                          #nullable disable
                                          namespace Microsoft.AspNetCore.Components
                                          {
                                              using System;
                                              using System.Threading.Tasks;

                                              public abstract class ComponentBase
                                              {
                                                  protected virtual void OnInitialized() { }
                                                  protected void StateHasChanged() { }
                                                  protected Task InvokeAsync(Action workItem) => Task.CompletedTask;
                                                  protected Task InvokeAsync(Func<Task> workItem) => Task.CompletedTask;
                                              }
                                          }
                                          """;

    /// <summary>Verifies a render requested from a threading-timer lambda callback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadingTimerLambdaIsReportedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using Microsoft.AspNetCore.Components;

                              public class Ticker : ComponentBase
                              {
                                  private Timer _timer;

                                  protected override void OnInitialized()
                                  {
                                      _timer = new Timer(_ => {|SST2710:StateHasChanged()|}, null, 0, 1000);
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render requested from a threading-timer method-group callback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadingTimerMethodGroupIsReportedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using Microsoft.AspNetCore.Components;

                              public class Ticker : ComponentBase
                              {
                                  private Timer _timer;

                                  protected override void OnInitialized()
                                  {
                                      _timer = new Timer(Tick, null, 0, 1000);
                                  }

                                  private void Tick(object state) => {|SST2710:StateHasChanged()|};
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render requested from a timers-timer <c>Elapsed</c> handler is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TimersTimerElapsedIsReportedAsync()
    {
        const string Source = """
                              using System.Timers;
                              using Microsoft.AspNetCore.Components;

                              public class Ticker : ComponentBase
                              {
                                  private readonly Timer _timer = new(1000);

                                  protected override void OnInitialized()
                                  {
                                      _timer.Elapsed += (s, e) => {|SST2710:StateHasChanged()|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render requested from a threading-timer block callback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadingTimerBlockLambdaIsReportedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using Microsoft.AspNetCore.Components;

                              public class Ticker : ComponentBase
                              {
                                  private Timer _timer;

                                  protected override void OnInitialized()
                                  {
                                      _timer = new Timer(_ =>
                                      {
                                          {|SST2710:StateHasChanged()|};
                                      }, null, 0, 1000);
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the method-group <c>InvokeAsync(StateHasChanged)</c> form is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MarshalledMethodGroupIsSilentAsync()
    {
        const string Source = """
                              using System.Threading;
                              using Microsoft.AspNetCore.Components;

                              public class Ticker : ComponentBase
                              {
                                  private Timer _timer;

                                  protected override void OnInitialized()
                                  {
                                      _timer = new Timer(_ => InvokeAsync(StateHasChanged), null, 0, 1000);
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render wrapped in <c>InvokeAsync(() =&gt; StateHasChanged())</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MarshalledLambdaIsSilentAsync()
    {
        const string Source = """
                              using System.Threading;
                              using Microsoft.AspNetCore.Components;

                              public class Ticker : ComponentBase
                              {
                                  private Timer _timer;

                                  protected override void OnInitialized()
                                  {
                                      _timer = new Timer(_ => InvokeAsync(() => StateHasChanged()), null, 0, 1000);
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a non-timer call site is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTimerCallSiteIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Ticker : ComponentBase
                              {
                                  protected override void OnInitialized()
                                  {
                                      Run(() => StateHasChanged());
                                  }

                                  private void Run(Action action) => action();
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a same-named method on a non-component type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonComponentTimerIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using Microsoft.AspNetCore.Components;

                              public class Plain
                              {
                                  private Timer _timer;

                                  public void Start()
                                  {
                                      _timer = new Timer(_ => StateHasChanged(), null, 0, 1000);
                                  }

                                  private void StateHasChanged() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the rule stays silent when no component assembly is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenComponentBaseNotReferencedAsync()
    {
        const string Source = """
                              using System.Threading;

                              namespace Look
                              {
                                  public abstract class ComponentBase
                                  {
                                      protected virtual void OnInitialized() { }
                                      protected void StateHasChanged() { }
                                  }

                                  public class Ticker : ComponentBase
                                  {
                                      private Timer _timer;

                                      protected override void OnInitialized()
                                      {
                                          _timer = new Timer(_ => StateHasChanged(), null, 0, 1000);
                                      }
                                  }
                              }
                              """;

        var test = new VerifyTimer.Test
        {
            TestCode = Source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the component marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyTimer.Test
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("ComponentBaseStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
