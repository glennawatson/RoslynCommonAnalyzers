// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLifecycle = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2711AsyncVoidLifecycleOverrideAnalyzer,
    StyleSharp.Analyzers.Sst2711AsyncVoidLifecycleOverrideCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="Sst2711AsyncVoidLifecycleOverrideCodeFixProvider"/> (SST2711 override the Task-returning
/// lifecycle twin).
/// </summary>
public class Sst2711AsyncVoidLifecycleOverrideCodeFixUnitTest
{
    /// <summary>The component marker stub added to both the test and fixed documents so the marker resolves.</summary>
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

    /// <summary>Verifies an async void OnInitialized override becomes an async Task OnInitializedAsync override.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RewritesOnInitializedToTaskTwinAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override async void {|SST2711:OnInitialized|}() => await Task.Delay(1);
                              }
                              """;
        const string Fixed = """
                             using System.Threading.Tasks;
                             using Microsoft.AspNetCore.Components;

                             public class Counter : ComponentBase
                             {
                                 protected override async Task OnInitializedAsync() => await Task.Delay(1);
                             }
                             """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies the parameter of OnAfterRender is preserved when rewriting to the twin.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RewritesOnAfterRenderPreservingParameterAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override async void {|SST2711:OnAfterRender|}(bool firstRender) => await Task.Delay(1);
                              }
                              """;
        const string Fixed = """
                             using System.Threading.Tasks;
                             using Microsoft.AspNetCore.Components;

                             public class Counter : ComponentBase
                             {
                                 protected override async Task OnAfterRenderAsync(bool firstRender) => await Task.Delay(1);
                             }
                             """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies no fix is offered when the type already declares the twin, so the rename would collide.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoFixWhenTwinAlreadyDeclaredAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override async void {|SST2711:OnInitialized|}() => await Task.Delay(1);

                                  protected override Task OnInitializedAsync() => Task.CompletedTask;
                              }
                              """;
        await VerifyFixAsync(Source, Source);
    }

    /// <summary>Runs the code fix against the source plus the component marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyLifecycle.Test { TestCode = source, FixedCode = fixedSource };
        test.TestState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        test.FixedState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
