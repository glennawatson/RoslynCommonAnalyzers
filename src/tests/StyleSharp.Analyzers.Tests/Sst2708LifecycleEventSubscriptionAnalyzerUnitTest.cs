// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifySubscription = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2708LifecycleEventSubscriptionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2708LifecycleEventSubscriptionAnalyzer"/> (SST2708), which reports a component
/// that subscribes to an external event in a render-lifecycle method but never removes the subscription.
/// </summary>
public class Sst2708LifecycleEventSubscriptionAnalyzerUnitTest
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
                                                  protected virtual Task OnInitializedAsync() => Task.CompletedTask;
                                                  protected virtual void OnParametersSet() { }
                                                  protected virtual Task OnParametersSetAsync() => Task.CompletedTask;
                                                  protected virtual void OnAfterRender(bool firstRender) { }
                                                  protected virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
                                                  protected void StateHasChanged() { }
                                                  protected Task InvokeAsync(Action workItem) => Task.CompletedTask;
                                                  protected Task InvokeAsync(Func<Task> workItem) => Task.CompletedTask;
                                              }
                                          }
                                          """;

    /// <summary>Verifies a subscription in a lifecycle method with no disposal is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubscriptionWithoutDisposalIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Service { public event EventHandler Changed; }

                              public class Counter : ComponentBase
                              {
                                  private readonly Service _service = new();

                                  protected override void OnInitialized()
                                  {
                                      {|SST2708:_service.Changed|} += OnChanged;
                                  }

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an expression-bodied lifecycle subscription with no disposal is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedSubscriptionIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Service { public event EventHandler Changed; }

                              public class Counter : ComponentBase
                              {
                                  private readonly Service _service = new();

                                  protected override void OnInitialized() => {|SST2708:_service.Changed|} += OnChanged;

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an async lifecycle subscription with no disposal is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncLifecycleSubscriptionIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Service { public event EventHandler Changed; }

                              public class Counter : ComponentBase
                              {
                                  private readonly Service _service = new();

                                  protected override Task OnParametersSetAsync()
                                  {
                                      {|SST2708:_service.Changed|} += OnChanged;
                                      return Task.CompletedTask;
                                  }

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a subscription that is removed in <c>Dispose</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubscriptionRemovedInDisposeIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Service { public event EventHandler Changed; }

                              public class Counter : ComponentBase, IDisposable
                              {
                                  private readonly Service _service = new();

                                  protected override void OnInitialized()
                                  {
                                      _service.Changed += OnChanged;
                                  }

                                  public void Dispose()
                                  {
                                      _service.Changed -= OnChanged;
                                  }

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a subscription removed in <c>DisposeAsync</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubscriptionRemovedInDisposeAsyncIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Service { public event EventHandler Changed; }

                              public class Counter : ComponentBase, IAsyncDisposable
                              {
                                  private readonly Service _service = new();

                                  protected override void OnInitialized()
                                  {
                                      _service.Changed += OnChanged;
                                  }

                                  public ValueTask DisposeAsync()
                                  {
                                      _service.Changed -= OnChanged;
                                      return default;
                                  }

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the one event without a matching removal is reported while the removed one is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OnlyTheUnremovedEventIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Service
                              {
                                  public event EventHandler Changed;
                                  public event EventHandler Other;
                              }

                              public class Counter : ComponentBase, IDisposable
                              {
                                  private readonly Service _service = new();

                                  protected override void OnInitialized()
                                  {
                                      _service.Changed += OnChanged;
                                      {|SST2708:_service.Other|} += OnChanged;
                                  }

                                  public void Dispose()
                                  {
                                      _service.Changed -= OnChanged;
                                  }

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies subscribing to the component's own event is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OwnEventSubscriptionIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  public event EventHandler Changed;

                                  protected override void OnInitialized()
                                  {
                                      Changed += OnChanged;
                                  }

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a delegate-field <c>+=</c> that is not an event subscription is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateFieldAdditionIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Holder { public Action Callback; }

                              public class Counter : ComponentBase
                              {
                                  private readonly Holder _holder = new();

                                  protected override void OnInitialized()
                                  {
                                      _holder.Callback += OnCallback;
                                  }

                                  private void OnCallback() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a subscription outside any lifecycle method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubscriptionOutsideLifecycleIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Service { public event EventHandler Changed; }

                              public class Counter : ComponentBase
                              {
                                  private readonly Service _service = new();

                                  public void Wire()
                                  {
                                      _service.Changed += OnChanged;
                                  }

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a non-component type with a same-named method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonComponentTypeIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Service { public event EventHandler Changed; }

                              public class Plain
                              {
                                  private readonly Service _service = new();

                                  public void OnInitialized()
                                  {
                                      _service.Changed += OnChanged;
                                  }

                                  private void OnChanged(object sender, EventArgs e) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the rule stays silent when no component assembly is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The component stub is deliberately not added, so the real marker does not resolve and the analyzer
    /// registers nothing. The base type here is a look-alike in a non-component namespace, proving the gate
    /// rejects the shape on the marker type, not on the written name.
    /// </remarks>
    [Test]
    public async Task SilentWhenComponentBaseNotReferencedAsync()
    {
        const string Source = """
                              using System;

                              namespace Look
                              {
                                  public abstract class ComponentBase
                                  {
                                      protected virtual void OnInitialized() { }
                                  }

                                  public class Service { public event EventHandler Changed; }

                                  public class Counter : ComponentBase
                                  {
                                      private readonly Service _service = new();

                                      protected override void OnInitialized()
                                      {
                                          _service.Changed += OnChanged;
                                      }

                                      private void OnChanged(object sender, EventArgs e) { }
                                  }
                              }
                              """;

        var test = new VerifySubscription.Test
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
        var test = new VerifySubscription.Test
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("ComponentBaseStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
