// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Analyze = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1600RenderLoopDelegateAllocationAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1600 (a delegate captured per iteration inside a component render loop).</summary>
public class RenderLoopDelegateAllocationAnalyzerUnitTest
{
    /// <summary>Minimal Blazor stubs so the marker types resolve without the framework reference.</summary>
    private const string Stubs = """

                                 namespace Microsoft.AspNetCore.Components.Rendering
                                 {
                                     public sealed class RenderTreeBuilder
                                     {
                                         public void OpenElement(int sequence, string elementName) { }

                                         public void CloseElement() { }

                                         public void AddContent(int sequence, object value) { }

                                         public void AddAttribute(int sequence, string name, System.Action value) { }

                                         public void AddAttribute(int sequence, string name, Microsoft.AspNetCore.Components.EventCallback value) { }
                                     }
                                 }

                                 namespace Microsoft.AspNetCore.Components
                                 {
                                     using Microsoft.AspNetCore.Components.Rendering;

                                     public abstract class ComponentBase
                                     {
                                         protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }
                                     }

                                     public readonly struct EventCallback
                                     {
                                         public static readonly EventCallbackFactory Factory = new EventCallbackFactory();
                                     }

                                     public sealed class EventCallbackFactory
                                     {
                                         public EventCallback Create(object receiver, System.Action callback) => default;
                                     }
                                 }
                                 """;

    /// <summary>Verifies a lambda capturing the foreach iteration variable in a render loop is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEachLambdaCapturesIterationVariableReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", {|PSH1600:() => Select(item)|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a lambda capturing the for-loop counter directly is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForLoopLambdaCapturesCounterReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      for (int i = 0; i < _items.Count; i++)
                                      {
                                          builder.AddAttribute(1, "onclick", {|PSH1600:() => Select(i)|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a lambda capturing a local declared in the loop body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForLoopLambdaCapturesBodyLocalReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      for (int i = 0; i < _items.Count; i++)
                                      {
                                          var index = i;
                                          builder.AddAttribute(1, "onclick", {|PSH1600:() => Select(index)|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the event-callback factory shape produced by a .razor component's @onclick is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventCallbackFactoryLambdaCapturesReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, {|PSH1600:() => Select(item)|}));
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a block-bodied anonymous method capturing the iteration variable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnonymousMethodCapturesIterationVariableReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", {|PSH1600:delegate { Select(item); }|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies only the outer render fragment is reported when a nested lambda also reads the loop variable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedCapturingLambdaReportsOnlyOuterAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  private static void Run(Action work) => work();

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", {|PSH1600:() => Run(() => Select(item))|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a loop-invariant lambda that captures nothing loop-scoped is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoopInvariantLambdaNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", () => Select(0));
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a lambda that captures only a field (not a loop local) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldCapturingLambdaNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private int _selected;

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", () => Select(_selected));
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a method group handler in a render loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupHandlerNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Refresh() { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", Refresh);
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a delegate hoisted out of the loop and reused inside it is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HoistedDelegateNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      Action handler = () => Select(0);
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", handler);
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a capturing lambda outside any loop in the render method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaOutsideLoopNotReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      var only = 5;
                                      builder.AddAttribute(1, "onclick", () => Select(only));
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a lambda that only reads its own parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaReadingOwnParameterNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          Func<int, int> map = x => x + 1;
                                          builder.AddContent(1, map);
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a capturing lambda inside a local function in the render loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaInsideLocalFunctionNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          void Attach() => builder.AddAttribute(1, "onclick", () => Select(item));
                                          Attach();
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the same capturing shape in a method that is not a render method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturingLambdaInNonRenderMethodNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  public void Wire(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", () => Select(item));
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a BuildRenderTree whose single parameter is not the render-tree builder is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BuildRenderTreeWithWrongParameterTypeNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class NotAComponent
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  private void BuildRenderTree(int builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          System.Action handler = () => Select(item);
                                          handler();
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a capturing lambda in a render loop inside a constructor (no containing method) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturingLambdaInConstructorNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  public Rows()
                                  {
                                      foreach (var item in _items)
                                      {
                                          Action handler = () => Select(item);
                                          handler();
                                      }
                                  }

                                  private void Select(int value) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies nothing is reported when the Blazor render-tree builder type is not referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBlazorReferenceNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class RenderTreeBuilder
                              {
                                  public void AddAttribute(int sequence, string name, Action value) { }
                              }

                              public sealed class Widget
                              {
                                  private readonly List<int> _items = new();

                                  private void Select(int value) { }

                                  private void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddAttribute(1, "onclick", () => Select(item));
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

    /// <summary>Runs the analyzer against the source plus the Blazor stubs on the .NET 9 reference assemblies.</summary>
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
