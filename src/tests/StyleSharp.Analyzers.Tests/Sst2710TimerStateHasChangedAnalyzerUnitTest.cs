// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using VerifyTimer = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2710TimerStateHasChangedAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2710TimerStateHasChangedAnalyzer"/> (SST2710), which reports
/// <c>StateHasChanged</c> called directly from a timer callback without marshalling onto the dispatcher.
/// </summary>
public class Sst2710TimerStateHasChangedAnalyzerUnitTest
{
    /// <summary>The component stub's document name.</summary>
    private const string ComponentStubPath = "ComponentBaseStub.cs";

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

        var test = new VerifyTimer.Test { TestCode = Source, ReferenceAssemblies = ReferenceAssemblies.Net.Net80, };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies all supported callback forms are scanned and dispatcher wrappers are recognized.</summary>
    /// <param name="creation">The timer wiring statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_ = new System.Threading.Timer(delegate(object state) { {|SST2710:StateHasChanged()|}; }, null, 0, 1);")]
    [Arguments("_ = new System.Threading.Timer((object state) => {|SST2710:StateHasChanged()|}, null, 0, 1);")]
    [Arguments("_ = new System.Threading.Timer(_ => this.InvokeAsync(() => StateHasChanged()), null, 0, 1);")]
    [Arguments("_ = new System.Threading.Timer(_ => ((System.Action)(() => {|SST2710:StateHasChanged()|}))(), null, 0, 1);")]
    [Arguments("System.Threading.Timer timer = new(_ => {|SST2710:StateHasChanged()|}, null, 0, 1);")]
    [Arguments("var timer = new System.Timers.Timer(); timer.Elapsed += delegate(object sender, System.Timers.ElapsedEventArgs args) { {|SST2710:StateHasChanged()|}; };")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CallbackSyntaxDeterminesRenderScanAsync(string creation) => VerifyAsync(
        $$"""
        class Ticker : Microsoft.AspNetCore.Components.ComponentBase
        {
            void Start() { {{creation}} }
        }
        """);

    /// <summary>Verifies callbacks that do not resolve to a same-file method are not scanned.</summary>
    /// <param name="members">The callback declaration and timer wiring.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void Start() { System.Threading.TimerCallback callback = _ => StateHasChanged(); _ = new System.Threading.Timer(callback); }")]
    [Arguments("void Start() { void Tick(object state) => StateHasChanged(); _ = new System.Threading.Timer(Tick); }")]
    [Arguments("void Start() { _ = new System.Threading.Timer(System.GC.KeepAlive); }")]
    [Arguments("void Start() { _ = new object(); _ = new System.Action(StateHasChanged); }")]
    [Arguments("void Start() { var timer = new System.Timers.Timer(); timer.Elapsed += null; }")]
    [Arguments("void Start() { var timer = new System.Timers.Timer(); timer.Disposed += (s, e) => StateHasChanged(); }")]
    [Arguments("event System.Action Elapsed; void Start() { Elapsed += () => StateHasChanged(); }")]
    [Arguments("event System.Action Elapsed; void Start() { this.Elapsed += () => StateHasChanged(); }")]
    [Arguments("System.Action Elapsed; void Start() { this.Elapsed += () => StateHasChanged(); }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedWiringIsSilentAsync(string members) => VerifyAsync(
        $$"""
        class Ticker : Microsoft.AspNetCore.Components.ComponentBase
        {
            {{members}}
        }
        """);

    /// <summary>Verifies a qualified same-file callback reports its direct render request.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task QualifiedMethodGroupIsReportedAsync() => VerifyAsync(
        """
        class Ticker : Microsoft.AspNetCore.Components.ComponentBase
        {
            void Start() { _ = new System.Threading.Timer(this.Tick); }
            void Tick(object state) => {|SST2710:StateHasChanged()|};
        }
        """);

    /// <summary>Verifies a method-group target in another document is outside the scan boundary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MethodGroupInAnotherDocumentIsSilentAsync()
    {
        var test = new VerifyTimer.Test
        {
            TestCode = "partial class Ticker : Microsoft.AspNetCore.Components.ComponentBase { void Start() { _ = new System.Threading.Timer(Tick); } }",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add((ComponentStubPath, ComponentsStub));
        test.TestState.Sources.Add(("Callback.cs", "partial class Ticker { void Tick(object state) => StateHasChanged(); }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies event subscriptions without the component model stay silent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ElapsedSubscriptionWithoutComponentModelIsSilentAsync()
    {
        var test = new VerifyTimer.Test
        {
            TestCode = "class C { void Start() { var timer = new System.Timers.Timer(); timer.Elapsed += (s, e) => StateHasChanged(); } void StateHasChanged() { } }",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies missing timer metadata stops analysis before binding the callback.</summary>
    /// <param name="statement">The timer candidate whose framework type is unavailable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_ = new Timer(Callback);")]
    [Arguments("timer.Elapsed += Callback;")]
    public async Task MissingTimerFrameworkTypesAreSilentAsync(string statement)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            namespace Microsoft.AspNetCore.Components { class ComponentBase { } }
            class C { void M() { {{statement}} } void Callback() { } }
            """);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2710TimerStateHasChangedAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies unresolved constructors and event symbols are ignored in incomplete source.</summary>
    /// <param name="statement">The unresolved timer wiring.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_ = new System.Threading.Timer(Missing);")]
    [Arguments("missing.Elapsed += (s, e) => StateHasChanged();")]
    public async Task UnresolvedTimerWiringIsSilentAsync(string statement)
    {
        var test = new VerifyTimer.Test
        {
            TestCode = $"class Ticker : Microsoft.AspNetCore.Components.ComponentBase {{ void Start() {{ {statement} }} }}",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.TestState.Sources.Add((ComponentStubPath, ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the component marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyTimer.Test { TestCode = source, ReferenceAssemblies = ReferenceAssemblies.Net.Net80, };
        test.TestState.Sources.Add((ComponentStubPath, ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
