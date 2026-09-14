// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

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

    /// <summary>Cached references for a framework without LINQ.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies both kinds of nested function stop the enclosing-loop search.</summary>
    /// <param name="statement">The nested function containing the allocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.Action later = () => builder.AddComponentParameter(0, \"Data\", new object());")]
    [Arguments("void Later() { builder.AddComponentParameter(0, \"Data\", new object()); }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NestedFunctionAllocationIsCleanAsync(string statement) =>
        VerifyAsync(
            $$"""
            using Microsoft.AspNetCore.Components.Rendering;
            class C
            {
                void BuildRenderTree(RenderTreeBuilder builder)
                {
                    for (int i = 0; i < 2; i++) { {{statement}} }
                }
            }
            """);

    /// <summary>Verifies parentheses retain allocation recognition for each materializer.</summary>
    /// <param name="value">The allocated parameter value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("((new object()))")]
    [Arguments("items.ToArray()")]
    [Arguments("items.ToHashSet()")]
    [Arguments("items.ToDictionary(item => item)")]
    [Arguments("items.ToLookup(item => item)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task WrappedAllocationAndMaterializersAreReportedAsync(string value) =>
        VerifyAsync(
            $$"""
            using System.Linq;
            using Microsoft.AspNetCore.Components.Rendering;
            class C
            {
                void BuildRenderTree(RenderTreeBuilder builder)
                {
                    int[] items = { 1, 2 };
                    foreach (var item in items)
                    {
                        builder.AddComponentParameter(0, "Data", {|PSH1603:{{value}}|});
                    }
                }
            }
            """);

    /// <summary>Verifies loops in constructors and methods with extra parameters are not render loops.</summary>
    /// <param name="signature">The containing member's signature.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public C(RenderTreeBuilder builder)")]
    [Arguments("void BuildRenderTree(RenderTreeBuilder builder, int count)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonRenderMemberIsCleanAsync(string signature) =>
        VerifyAsync(
            $$"""
            using Microsoft.AspNetCore.Components.Rendering;
            class C
            {
                {{signature}}
                {
                    for (int i = 0; i < 2; i++) { builder.AddComponentParameter(0, "Data", new object()); }
                }
            }
            """);

    /// <summary>Verifies a varargs marker does not satisfy the semantic render-parameter requirement.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task VarargsRenderMethodIsCleanAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Rendering;
            class C
            {
                void BuildRenderTree(__arglist)
                {
                    var builder = new RenderTreeBuilder();
                    for (int i = 0; i < 2; i++) { builder.AddComponentParameter(0, "Data", new object()); }
                }
            }
            """);

    /// <summary>Verifies same-named receivers and calls with fewer than three arguments are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonBuilderReceiverAndShortCallAreCleanAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Rendering;
            class C
            {
                void BuildRenderTree(RenderTreeBuilder builder)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        this.AddComponentParameter(0, "Data", new object());
                        this.AddComponentParameter(0, "Data");
                    }
                }
                void AddComponentParameter(int sequence, string name, object value = null) { }
            }
            """);

    /// <summary>Verifies top-level code has neither a containing type nor a render method.</summary>
    /// <param name="statement">The top-level allocation statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("builder.AddComponentParameter(0, \"Data\", new object());")]
    [Arguments("for (int i = 0; i < 2; i++) { builder.AddComponentParameter(0, \"Data\", new object()); }")]
    public async Task TopLevelAllocationIsCleanAsync(string statement)
    {
        var tree = CSharpSyntaxTree.ParseText($"var builder = new Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder(); {statement}{Stubs}");
        var compilation = CSharpCompilation.Create("TopLevelRender", [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1603RenderLoopParameterAllocationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies an incomplete assembly attribute reaches the compilation root without finding a render loop.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AssemblyAttributeAllocationIsCleanAsync()
    {
        const string Source = """
            using System;
            using Microsoft.AspNetCore.Components.Rendering;
            [assembly: Flag(new RenderTreeBuilder().AddComponentParameter(0, "Data", new object()))]
            class FlagAttribute : Attribute { public FlagAttribute(object value) { } }
            """ + Stubs;
        var compilation = CSharpCompilation.Create("AttributeRender", [CSharpSyntaxTree.ParseText(Source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1603RenderLoopParameterAllocationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies missing query metadata does not turn a custom materializer into a LINQ allocation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingEnumerableMetadataIsCleanAsync()
    {
        const string Source = """
            using Microsoft.AspNetCore.Components.Rendering;
            class C
            {
                void BuildRenderTree(RenderTreeBuilder builder)
                {
                    for (int i = 0; i < 2; i++) { builder.AddComponentParameter(0, "Data", this.ToList()); }
                }
                object ToList() => null;
            }
            """ + Stubs;
        var compilation = CSharpCompilation.Create("MissingEnumerable", [CSharpSyntaxTree.ParseText(Source)], CoreReferences);
        var diagnostics = await compilation.WithAnalyzers([new Psh1603RenderLoopParameterAllocationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies incomplete receivers and unresolved materializers do not produce a render recommendation.</summary>
    /// <param name="statement">The incomplete call under analysis.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("missing.AddComponentParameter(0, \"Data\", new object());")]
    [Arguments("builder.AddComponentParameter(0, \"Data\", items.ToList(1));")]
    public async Task UnresolvedRenderCallIsCleanAsync(string statement)
    {
        var source = $$"""
            using System.Linq;
            using Microsoft.AspNetCore.Components.Rendering;
            class C
            {
                void BuildRenderTree(RenderTreeBuilder builder)
                {
                    int[] items = { 1 };
                    foreach (var item in items) { {{statement}} }
                }
            }
            """ + Stubs;
        var compilation = CSharpCompilation.Create("UnresolvedRender", [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1603RenderLoopParameterAllocationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

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
        var test = new Analyze.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the Blazor stubs on the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Analyze.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = $"{source}\n{Stubs}", };

        await test.RunAsync(CancellationToken.None);
    }
}
