// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLifecycle = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2711AsyncVoidLifecycleOverrideAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2711AsyncVoidLifecycleOverrideAnalyzer"/> (SST2711), which reports a
/// <c>ComponentBase</c> override of a synchronous lifecycle method declared <c>async void</c>.
/// </summary>
public class Sst2711AsyncVoidLifecycleOverrideAnalyzerUnitTest
{
    /// <summary>
    /// An in-source stub of the component base type — carrying every synchronous hook and its Task-returning twin
    /// — added as a second document so the marker resolves without a package restore. Omitting it is what the
    /// "not referenced" gate test relies on.
    /// </summary>
    private const string ComponentsStub = """
                                          namespace Microsoft.AspNetCore.Components
                                          {
                                              public abstract class ComponentBase
                                              {
                                                  protected virtual void OnInitialized() { }
                                                  protected virtual System.Threading.Tasks.Task OnInitializedAsync() => System.Threading.Tasks.Task.CompletedTask;
                                                  protected virtual void OnParametersSet() { }
                                                  protected virtual System.Threading.Tasks.Task OnParametersSetAsync() => System.Threading.Tasks.Task.CompletedTask;
                                                  protected virtual void OnAfterRender(bool firstRender) { }
                                                  protected virtual System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender) => System.Threading.Tasks.Task.CompletedTask;
                                              }
                                          }
                                          """;

    /// <summary>Verifies an async void override of OnInitialized is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidOnInitializedIsReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override async void {|SST2711:OnInitialized|}() => await Task.Delay(1);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an async void override of OnParametersSet is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidOnParametersSetIsReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override async void {|SST2711:OnParametersSet|}() => await Task.Delay(1);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an async void override of OnAfterRender is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidOnAfterRenderIsReportedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override async void {|SST2711:OnAfterRender|}(bool firstRender) => await Task.Delay(1);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies overriding the Task-returning twin is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncTaskTwinIsSilentAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override async Task OnInitializedAsync() => await Task.Delay(1);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a plain synchronous override is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SynchronousVoidOverrideIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override void OnInitialized() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an async void method that is not a lifecycle override is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidNonLifecycleMethodIsSilentAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  public async void HandleClick() => await Task.Delay(1);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an async void method named like a hook on a non-component type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidLifecycleNameOutsideComponentIsSilentAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class Plain
                              {
                                  public async void OnInitialized() => await Task.Delay(1);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the rule stays silent when no component assembly is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The component stub is deliberately not added, so the marker does not resolve and the analyzer registers
    /// nothing. The base type here is a look-alike in a non-component namespace, proving the gate rejects the
    /// shape on the marker type, not on the written name.
    /// </remarks>
    [Test]
    public async Task SilentWhenComponentsNotReferencedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              namespace Look
                              {
                                  public abstract class ComponentBase
                                  {
                                      protected virtual void OnInitialized() { }
                                  }

                                  public class Counter : ComponentBase
                                  {
                                      protected override async void OnInitialized() => await Task.Delay(1);
                                  }
                              }
                              """;

        var test = new VerifyLifecycle.Test { TestCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the component marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyLifecycle.Test { TestCode = source };
        test.TestState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
