// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Analyze = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1603RenderLoopParameterAllocationAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1603 (a non-delegate allocation as a component parameter in a render loop).</summary>
public class RenderLoopParameterAllocationAnalyzerUnitTest
{
    /// <summary>Minimal Blazor stubs so the marker types resolve without the framework reference.</summary>
    private const string Stubs = """

                                 namespace Microsoft.AspNetCore.Components.Rendering
                                 {
                                     public sealed class RenderTreeBuilder
                                     {
                                         public void AddComponentParameter(int sequence, string name, object value) { }
                                     }
                                 }

                                 namespace Microsoft.AspNetCore.Components
                                 {
                                     using Microsoft.AspNetCore.Components.Rendering;

                                     public abstract class ComponentBase
                                     {
                                         protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }
                                     }
                                 }
                                 """;

    /// <summary>Verifies a new collection passed as a component parameter in a foreach is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewCollectionParameterReportedAsync()
    {
        const string Source = """
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
                                          builder.AddComponentParameter(1, "Data", {|PSH1603:new List<int> { item }|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an implicit array passed as a component parameter in a for loop is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitArrayParameterReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      for (int i = 0; i < _items.Count; i++)
                                      {
                                          builder.AddComponentParameter(1, "Data", {|PSH1603:new[] { _items[i] }|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an explicit array passed as a component parameter in a foreach is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitArrayParameterReportedAsync()
    {
        const string Source = """
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
                                          builder.AddComponentParameter(1, "Data", {|PSH1603:new int[] { item }|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a target-typed new passed as a component parameter in a foreach is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitObjectCreationParameterReportedAsync()
    {
        const string Source = """
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
                                          builder.AddComponentParameter(1, "Data", {|PSH1603:new()|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a materializing query passed as a component parameter in a foreach is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaterializingQueryParameterReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddComponentParameter(1, "Data", {|PSH1603:_items.ToList()|});
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a delegate creation passed as a component parameter is not reported (PSH1600's concern).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateCreationNotReportedAsync()
    {
        const string Source = """
                              using System;
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
                                          builder.AddComponentParameter(1, "OnClick", new Action(Refresh));
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a plain non-allocating value passed as a component parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainValueParameterNotReportedAsync()
    {
        const string Source = """
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
                                          builder.AddComponentParameter(1, "Data", item);
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies non-materializing calls passed as component parameters are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonMaterializingCallNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private int Compute(int value) => value;

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddComponentParameter(1, "A", Compute(item));
                                          builder.AddComponentParameter(2, "B", _items.IndexOf(item));
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a same-named non-query materializer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomToListNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Bag
                              {
                                  public object ToList() => new object();
                              }

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private readonly Bag _bag = new();

                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddComponentParameter(1, "Data", _bag.ToList());
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an allocation outside any loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllocationOutsideLoopNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  protected override void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      builder.AddComponentParameter(1, "Data", new List<int>());
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the same allocation in a loop outside a render method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllocationOutsideRenderMethodNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  public void Wire(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddComponentParameter(1, "Data", new List<int> { item });
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a BuildRenderTree whose single parameter is not the render-tree builder is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrongBuilderParameterTypeNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Components;
                              using Microsoft.AspNetCore.Components.Rendering;

                              public sealed class Rows : ComponentBase
                              {
                                  private readonly List<int> _items = new();

                                  private readonly RenderTreeBuilder _builder = new();

                                  private void BuildRenderTree(string name)
                                  {
                                      foreach (var item in _items)
                                      {
                                          _builder.AddComponentParameter(1, "Data", new List<int> { item });
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies nothing is reported when the render-tree builder type is not referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBlazorReferenceNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class RenderTreeBuilder
                              {
                                  public void AddComponentParameter(int sequence, string name, object value) { }
                              }

                              public sealed class Rows
                              {
                                  private readonly List<int> _items = new();

                                  private void BuildRenderTree(RenderTreeBuilder builder)
                                  {
                                      foreach (var item in _items)
                                      {
                                          builder.AddComponentParameter(1, "Data", new List<int> { item });
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
