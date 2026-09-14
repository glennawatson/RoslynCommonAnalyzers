// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Analyze = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1502LazyEnumerableRouteResultAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1502 (a route handler returns a deferred sequence that serializes synchronously).</summary>
public class LazyEnumerableRouteResultAnalyzerUnitTest
{
    /// <summary>Minimal ASP.NET Core stubs so the marker types resolve without the framework reference.</summary>
    private const string Stubs = """

                                 namespace Microsoft.AspNetCore.Builder
                                 {
                                     public interface IEndpointRouteBuilder { }

                                     public sealed class WebApplication : IEndpointRouteBuilder { }

                                     public static class EndpointRouteBuilderExtensions
                                     {
                                         public static void MapGet(this IEndpointRouteBuilder builder, string pattern, System.Delegate handler) { }
                                         public static void MapPost(this IEndpointRouteBuilder builder, string pattern, System.Delegate handler) { }
                                         public static void MapPut(this IEndpointRouteBuilder builder, string pattern, System.Delegate handler) { }
                                         public static void MapDelete(this IEndpointRouteBuilder builder, string pattern, System.Delegate handler) { }
                                         public static void MapPatch(this IEndpointRouteBuilder builder, string pattern, System.Delegate handler) { }
                                         public static void Map(this IEndpointRouteBuilder builder, string pattern, System.Delegate handler) { }
                                     }
                                 }

                                 namespace Microsoft.AspNetCore.Mvc
                                 {
                                     public abstract class ControllerBase { }

                                     public sealed class ApiControllerAttribute : System.Attribute { }

                                     public sealed class RouteAttribute : System.Attribute
                                     {
                                         public RouteAttribute(string template) { }
                                     }

                                     public sealed class NonActionAttribute : System.Attribute { }

                                     public sealed class HttpGetAttribute : System.Attribute
                                     {
                                         public HttpGetAttribute() { }

                                         public HttpGetAttribute(string template) { }
                                     }
                                 }

                                 public class Widget
                                 {
                                     public bool Active { get; set; }

                                     public string Name { get; set; } = "";

                                     public int Id { get; set; }
                                 }
                                 """;

    /// <summary>The cached reference set for compilations without LINQ assemblies.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies deferred operator names are matched exactly and case-sensitively.</summary>
    /// <param name="name">The operator name.</param>
    /// <param name="expected">Whether the operator is deferred.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Where", true)]
    [Arguments("Select", true)]
    [Arguments("SelectMany", true)]
    [Arguments("OrderBy", true)]
    [Arguments("OrderByDescending", true)]
    [Arguments("ThenBy", true)]
    [Arguments("ThenByDescending", true)]
    [Arguments("", false)]
    [Arguments("where", false)]
    [Arguments("WherE", false)]
    [Arguments("select", false)]
    [Arguments("SelecT", false)]
    [Arguments("selectMany", false)]
    [Arguments("SelectManY", false)]
    [Arguments("orderBy", false)]
    [Arguments("OrderBY", false)]
    [Arguments("orderByDescending", false)]
    [Arguments("OrderByDescendinG", false)]
    [Arguments("thenBy", false)]
    [Arguments("ThenBY", false)]
    [Arguments("thenByDescending", false)]
    [Arguments("ThenByDescendinG", false)]
    public async Task DeferredOperatorNamesAreExactAsync(string name, bool expected)
    {
        var actual = Psh1502LazyEnumerableRouteResultAnalyzer.IsDeferredOperatorName(name);
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Verifies all route mapping names recognize a deferred handler result.</summary>
    /// <param name="map">The route mapping method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("MapGet")]
    [Arguments("MapPost")]
    [Arguments("MapPut")]
    [Arguments("MapDelete")]
    [Arguments("MapPatch")]
    [Arguments("Map")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RouteMappingNamesReportDeferredResultsAsync(string map) =>
        VerifyAsync($$"""
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.AspNetCore.Builder;
            class C
            {
                void M(WebApplication app, List<Widget> items) =>
                    app.{{map}}("/", () => {|PSH1502:items.Where(w => w.Active)|});
            }
            """);

    /// <summary>Verifies aliases resolve enumerable return types and action attributes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AliasedReturnTypeAndAttributesAreRecognizedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            using Sequences = System.Collections.Generic;
            using Mvc = Microsoft.AspNetCore.Mvc;
            class C : Mvc.ControllerBase
            {
                readonly List<Widget> items = new();
                [Mvc::HttpGet]
                public Sequences::IEnumerable<Widget> Get() => {|PSH1502:items.Where(w => w.Active)|};
                [System.Obsolete, Microsoft.AspNetCore.Mvc.NonActionAttribute]
                public IEnumerable<Widget> Qualified() => items.Where(w => w.Active);
                [System.Obsolete]
                [Mvc::NonAction]
                public IEnumerable<Widget> Aliased() => items.Where(w => w.Active);
            }
            """);

    /// <summary>Verifies every deferred LINQ operator is distinguished from materializing and custom methods.</summary>
    /// <param name="expression">The returned expression with any expected diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{|PSH1502:items.SelectMany(w => items)|}")]
    [Arguments("{|PSH1502:items.OrderBy(w => w.Id)|}")]
    [Arguments("{|PSH1502:items.OrderBy(w => w.Id).ThenByDescending(w => w.Name)|}")]
    [Arguments("{|PSH1502:Enumerable.Where(items, w => w.Active)|}")]
    [Arguments("items.ToArray()")]
    [Arguments("Enumerable.Empty<Widget>()")]
    [Arguments("Where(items)")]
    [Arguments("null")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DeferredOperatorClassificationAsync(string expression) =>
        VerifyAsync($$"""
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.AspNetCore.Mvc;
            class C : ControllerBase
            {
                readonly List<Widget> items = new();
                public IEnumerable<Widget> Get() => {{expression}};
                IEnumerable<Widget> Where(IEnumerable<Widget> value) => value;
            }
            """);

    /// <summary>Verifies only a single task wrapper around an enumerable return is accepted.</summary>
    /// <param name="handler">The handler lambda.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("() => items.ToArray()")]
    [Arguments("() => items")]
    [Arguments("() => 1")]
    [Arguments("() => { }")]
    [Arguments("async () => { await Task.Yield(); return items.ToList(); }")]
    [Arguments("() => Task.FromResult(Task.FromResult<IEnumerable<Widget>>(items))")]
    [Arguments("async () => { await Task.Yield(); return {|PSH1502:items.Where(w => w.Active)|}; }")]
    [Arguments("async ValueTask<IEnumerable<Widget>> () => { await Task.Yield(); return {|PSH1502:items.Where(w => w.Active)|}; }")]
    [Arguments("(List<Widget> values) => {|PSH1502:values.Where(w => w.Active)|}")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task HandlerReturnShapesAreClassifiedAsync(string handler) =>
        VerifyAsync($$"""
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            class C
            {
                void M(WebApplication app, List<Widget> items) => app.MapGet("/", {{handler}});
            }
            """);

    /// <summary>Verifies namespace lookalikes cannot make a custom map method an endpoint registration.</summary>
    /// <param name="routeNamespace">The namespace containing the custom mapping method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Other.Builder")]
    [Arguments("Other.AspNetCore.Builder")]
    [Arguments("Other.Microsoft.AspNetCore.Builder")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RoutingNamespaceLookalikesAreIgnoredAsync(string routeNamespace) =>
        VerifyAsync($$"""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<Widget> items) => {{routeNamespace}}.Routes.MapGet("/", () => items.Where(w => w.Active));
            }
            namespace {{routeNamespace}}
            {
                static class Routes { public static void MapGet(string pattern, System.Delegate handler) { } }
            }
            """);

    /// <summary>Verifies returns inside all nested anonymous function shapes are excluded from the action scan.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NestedAnonymousReturnsAreIgnoredAsync() =>
        VerifyAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.AspNetCore.Mvc;
            class C : ControllerBase
            {
                public IEnumerable<Widget> Get(List<Widget> items)
                {
                    Func<int, IEnumerable<Widget>> simple = x => { return items.Where(w => w.Active); };
                    Func<IEnumerable<Widget>> parenthesized = () => { return items.Where(w => w.Active); };
                    Func<IEnumerable<Widget>> anonymous = delegate { return items.Where(w => w.Active); };
                    return items;
                }
            }
            """);

    /// <summary>Verifies incomplete bindings do not throw and queryable marker returns retain their current classification.</summary>
    /// <param name="member">The member being edited.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public IEnumerable<Widget> Get() => {|PSH1502:(IQueryable)null|};")]
    [Arguments("public IEnumerable<Widget> Get() => Missing();")]
    [Arguments("public IEnumerable<Widget> Get() { return; }")]
    [Arguments("public IEnumerable<Widget> Get();")]
    [Arguments("public Task<Widget> Get() => null;")]
    [Arguments("public IEnumerable<Widget>? Get() => items.Where(w => w.Active);")]
    [Arguments("public IEnumerable<Widget> I.Get() => items.Where(w => w.Active); interface I { IEnumerable<Widget> Get(); }")]
    [Arguments("public void Configure(WebApplication app) => app.MapGet(\"/\", () => items.Where(w => w.Active), 0);")]
    public async Task IncompleteAndNullableActionsAreClassifiedAsync(string member)
    {
        var test = new Analyze.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $$"""
                using System.Collections.Generic;
                using System.Linq;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Mvc;
                class C : ControllerBase
                {
                    readonly List<Widget> items = new();
                    {{member}}
                }
                {{Stubs}}
                """,
        };
        await test.RunAsync();
    }

    /// <summary>Verifies an MVC reference alone does not enable minimal API handler analysis.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingWebApplicationDisablesMappingAsync()
    {
        var test = new Analyze.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = """
                using System.Collections.Generic;
                using System.Linq;
                using Microsoft.AspNetCore.Builder;
                class C
                {
                    void M(List<int> items) => Routes.MapGet("/", () => items.Where(x => x > 0));
                }
                namespace Microsoft.AspNetCore.Mvc { public class ControllerBase { } }
                namespace Microsoft.AspNetCore.Builder
                {
                    public static class Routes { public static void MapGet(string path, System.Delegate handler) { } }
                }
                """,
        };
        await test.RunAsync();
    }

    /// <summary>Verifies the minimal API marker enables mapping analysis without enabling MVC action analysis.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WebApplicationWithoutMvcAnalyzesOnlyMappingsAsync()
    {
        var test = new Analyze.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = """
                using System.Collections.Generic;
                using System.Linq;
                using Microsoft.AspNetCore.Builder;
                class C
                {
                    public IEnumerable<int> Get(List<int> items) => items.Where(x => x > 0);
                    void M(WebApplication app, List<int> items) => app.MapGet("/", () => {|PSH1502:items.Where(x => x > 0)|});
                }
                namespace Microsoft.AspNetCore.Builder
                {
                    public class WebApplication { public void MapGet(string path, System.Delegate handler) { } }
                }
                """,
        };
        await test.RunAsync();
    }

    /// <summary>Verifies missing LINQ metadata does not prevent classifying an ordinary enumerable return.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingLinqTypesAreIgnoredAsync()
    {
        const string Source = """
            namespace Microsoft.AspNetCore.Mvc { public class ControllerBase { } }
            class C : Microsoft.AspNetCore.Mvc.ControllerBase
            {
                public System.Collections.Generic.IEnumerable<int> Get() => new int[0];
            }
            """;
        var compilation = CSharpCompilation.Create(
            nameof(MissingLinqTypesAreIgnoredAsync),
            [CSharpSyntaxTree.ParseText(Source)],
            CoreReferences,
            new(OutputKind.DynamicallyLinkedLibrary));
        await Assert.That(compilation.GetDiagnostics()).IsEmpty();
        var diagnostics = await compilation.WithAnalyzers([new Psh1502LazyEnumerableRouteResultAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a minimal-API lambda returning a lazy in-memory LINQ query is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MinimalApiExpressionLambdaLazyEnumerableReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Builder;

                              public class Endpoints
                              {
                                  public void Configure(WebApplication app, List<Widget> items) =>
                                      app.MapGet("/widgets", () => {|PSH1502:items.Where(w => w.Active)|});
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a minimal-API lambda with an explicit IEnumerable return over an IQueryable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MinimalApiExplicitReturnQueryableReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Builder;

                              public class Endpoints
                              {
                                  private readonly IQueryable<Widget> _query = null!;

                                  public void Configure(WebApplication app) =>
                                      app.MapPost("/widgets", IEnumerable<Widget> () => {|PSH1502:_query.Where(w => w.Active)|});
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a minimal-API lambda with a block body and a deferred return is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MinimalApiBlockLambdaDeferredReturnReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Builder;

                              public class Endpoints
                              {
                                  public void Configure(WebApplication app, List<Widget> items) =>
                                      app.Map("/widgets", IEnumerable<Widget> () =>
                                      {
                                          return {|PSH1502:items.Select(w => w)|};
                                      });
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an expression-bodied controller action with a fully qualified return type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerExpressionBodiedLazyEnumerableReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Mvc;

                              [ApiController]
                              [Route("api/[controller]")]
                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  public void Ping() { }

                                  [HttpGet]
                                  public System.Collections.Generic.IEnumerable<Widget> Get() => {|PSH1502:_items.Where(w => w.Active)|};
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an abstract controller action declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractControllerActionNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using Microsoft.AspNetCore.Mvc;

                              public abstract class WidgetsControllerBase : ControllerBase
                              {
                                  public abstract IEnumerable<Widget> Get();
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an async Task-returning action whose block returns an IQueryable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerAsyncTaskQueryableReturnReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly IQueryable<Widget> _query = null!;

                                  public async Task<IEnumerable<Widget>> Get()
                                  {
                                      await Task.Yield();
                                      return {|PSH1502:_query.Where(w => w.Active)|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an async ValueTask-returning action whose block returns a lazy query is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerAsyncValueTaskLazyEnumerableReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  public async ValueTask<IEnumerable<Widget>> Get()
                                  {
                                      await Task.Yield();
                                      return {|PSH1502:_items.OrderByDescending(w => w.Name).ThenBy(w => w.Id)|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a deferred return nested inside an if-block is reported while a materialized return is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerNestedBlockDeferredReturnReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  public IEnumerable<Widget> Get()
                                  {
                                      if (_items.Count > 0)
                                      {
                                          return {|PSH1502:_items.Where(w => w.Active)|};
                                      }

                                      return _items;
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a materialized query typed as IEnumerable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerMaterializedQueryNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  public IEnumerable<Widget> Get() => _items.Where(w => w.Active).ToList();
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an IAsyncEnumerable-returning action, the correct streaming shape, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerAsyncEnumerableNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  public async IAsyncEnumerable<Widget> Get()
                                  {
                                      await Task.Yield();
                                      foreach (var item in _items)
                                      {
                                          yield return item;
                                      }
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a concrete List return type is not reported even when the value is a deferred query source.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerConcreteListReturnNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  public List<Widget> Get() => _items.Where(w => w.Active).ToList();
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a public method on a non-controller type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonControllerPublicMethodNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class WidgetService
                              {
                                  private readonly List<Widget> _items = new();

                                  public IEnumerable<Widget> Get() => _items.Where(w => w.Active);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a controller method marked NonAction is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerNonActionMethodNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  [NonAction]
                                  public IEnumerable<Widget> Helper() => _items.Where(w => w.Active);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a private controller method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerPrivateMethodNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  private IEnumerable<Widget> Get() => _items.Where(w => w.Active);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a static controller method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerStaticMethodNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private static readonly List<Widget> Items = new();

                                  public static IEnumerable<Widget> Get() => Items.Where(w => w.Active);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a nested local function's deferred return does not flag the enclosing action.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ControllerNestedLocalFunctionReturnNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Mvc;

                              public sealed class WidgetsController : ControllerBase
                              {
                                  private readonly List<Widget> _items = new();

                                  public IEnumerable<Widget> Get()
                                  {
                                      IEnumerable<Widget> Local()
                                      {
                                          return _items.Where(w => w.Active);
                                      }

                                      return Local().ToList();
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a similarly named map method outside the ASP.NET Core routing namespace is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedMapMethodNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public interface IRouter { }

                              public static class RouterExtensions
                              {
                                  public static void MapGet(this IRouter router, string pattern, System.Delegate handler) { }
                              }

                              public class Endpoints
                              {
                                  public void Configure(IRouter router, List<Widget> items) =>
                                      router.MapGet("/widgets", () => items.Where(w => w.Active));
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a map call handed a method group (not a lambda) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MinimalApiMethodGroupHandlerNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using Microsoft.AspNetCore.Builder;

                              public class Endpoints
                              {
                                  private static readonly List<Widget> Items = new();

                                  public void Configure(WebApplication app) => app.MapGet("/widgets", Handler);

                                  private static IEnumerable<Widget> Handler() => Items.Where(w => w.Active);
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies nothing is reported when neither ASP.NET Core marker type is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoAspNetCoreReferencesNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class Widget
                              {
                                  public bool Active { get; set; }
                              }

                              public class Service
                              {
                                  private readonly List<Widget> _items = new();

                                  public IEnumerable<Widget> Get() => _items.Where(w => w.Active);
                              }
                              """;
        var test = new Analyze.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the ASP.NET Core stubs on the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Analyze.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = $"{source}\n{Stubs}", };

        await test.RunAsync(CancellationToken.None);
    }
}
