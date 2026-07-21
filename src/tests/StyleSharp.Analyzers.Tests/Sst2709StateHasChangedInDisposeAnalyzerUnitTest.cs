// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyDispose = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2709StateHasChangedInDisposeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2709StateHasChangedInDisposeAnalyzer"/> (SST2709), which reports
/// <c>StateHasChanged</c> called from a component's <c>Dispose</c>/<c>DisposeAsync</c> body.
/// </summary>
public class Sst2709StateHasChangedInDisposeAnalyzerUnitTest
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

    /// <summary>Verifies a render requested from <c>Dispose</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateHasChangedInDisposeIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase, IDisposable
                              {
                                  public void Dispose()
                                  {
                                      {|SST2709:StateHasChanged()|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render requested through <c>this</c> from <c>Dispose</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateHasChangedThroughThisIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase, IDisposable
                              {
                                  public void Dispose()
                                  {
                                      {|SST2709:this.StateHasChanged()|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render requested from <c>DisposeAsync</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateHasChangedInDisposeAsyncIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Components;

                              public class Widget : ComponentBase, IAsyncDisposable
                              {
                                  public ValueTask DisposeAsync()
                                  {
                                      {|SST2709:StateHasChanged()|};
                                      return default;
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render requested from the protected <c>Dispose(bool)</c> pattern is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateHasChangedInDisposePatternIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase, IDisposable
                              {
                                  public void Dispose() => Dispose(true);

                                  protected virtual void Dispose(bool disposing)
                                  {
                                      {|SST2709:StateHasChanged()|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render requested from a lifecycle method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateHasChangedOutsideDisposalIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase
                              {
                                  protected override void OnInitialized()
                                  {
                                      StateHasChanged();
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a render deferred through a lambda inside <c>Dispose</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateHasChangedInsideLambdaIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Counter : ComponentBase, IDisposable
                              {
                                  public void Dispose()
                                  {
                                      Register(() => StateHasChanged());
                                  }

                                  private void Register(Action action) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a same-named method on a non-component type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateHasChangedOnNonComponentIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Plain : IDisposable
                              {
                                  public void Dispose()
                                  {
                                      StateHasChanged();
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
                              using System;

                              namespace Look
                              {
                                  public abstract class ComponentBase
                                  {
                                      protected void StateHasChanged() { }
                                  }

                                  public class Counter : ComponentBase, IDisposable
                                  {
                                      public void Dispose()
                                      {
                                          StateHasChanged();
                                      }
                                  }
                              }
                              """;

        var test = new VerifyDispose.Test
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
        var test = new VerifyDispose.Test
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(("ComponentBaseStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
