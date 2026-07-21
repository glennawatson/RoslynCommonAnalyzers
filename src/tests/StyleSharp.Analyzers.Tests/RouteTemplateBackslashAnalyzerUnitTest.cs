// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyRoute = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2700RouteTemplateBackslashAnalyzer,
    StyleSharp.Analyzers.Sst2700RouteTemplateBackslashCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2700 (a route template must not contain a backslash).</summary>
public class RouteTemplateBackslashAnalyzerUnitTest
{
    /// <summary>The inline stubs of the ASP.NET Core routing attributes the rule gates on.</summary>
    private const string RoutingStubs = """

        namespace Microsoft.AspNetCore.Mvc.Routing
        {
            public abstract class HttpMethodAttribute : System.Attribute
            {
                protected HttpMethodAttribute(System.Collections.Generic.IEnumerable<string> httpMethods) { }

                protected HttpMethodAttribute(System.Collections.Generic.IEnumerable<string> httpMethods, string template) { }
            }
        }

        namespace Microsoft.AspNetCore.Mvc
        {
            public sealed class RouteAttribute : System.Attribute
            {
                public RouteAttribute(string template) { }

                public string Name { get; set; } = "";
            }

            public sealed class HttpGetAttribute : Routing.HttpMethodAttribute
            {
                public HttpGetAttribute() : base(System.Array.Empty<string>()) { }

                public HttpGetAttribute(string template) : base(System.Array.Empty<string>(), template) { }
            }

            public sealed class HttpPostAttribute : Routing.HttpMethodAttribute
            {
                public HttpPostAttribute() : base(System.Array.Empty<string>()) { }

                public HttpPostAttribute(string template) : base(System.Array.Empty<string>(), template) { }
            }
        }
        """;

    /// <summary>A verbatim <c>[HttpGet]</c> route template that uses a backslash separator.</summary>
    private const string HttpGetVerbatimBackslashSource = """
        using Microsoft.AspNetCore.Mvc;

        public class Endpoints
        {
            [HttpGet({|SST2700:@"api\products"|})]
            public void Get() { }
        }
        """;

    /// <summary>The verbatim <c>[HttpGet]</c> template after the fix.</summary>
    private const string HttpGetVerbatimBackslashFixed = """
        using Microsoft.AspNetCore.Mvc;

        public class Endpoints
        {
            [HttpGet("api/products")]
            public void Get() { }
        }
        """;

    /// <summary>An escaped-backslash <c>[Route]</c> route template.</summary>
    private const string RouteEscapedBackslashSource = """
        using Microsoft.AspNetCore.Mvc;

        public class Endpoints
        {
            [Route({|SST2700:"api\\orders\\open"|})]
            public void Get() { }
        }
        """;

    /// <summary>The escaped-backslash <c>[Route]</c> template after the fix.</summary>
    private const string RouteEscapedBackslashFixed = """
        using Microsoft.AspNetCore.Mvc;

        public class Endpoints
        {
            [Route("api/orders/open")]
            public void Get() { }
        }
        """;

    /// <summary>A route template supplied through the named <c>template</c> argument.</summary>
    private const string NamedTemplateArgumentSource = """
        using Microsoft.AspNetCore.Mvc;

        public class Endpoints
        {
            [HttpPost(template: {|SST2700:@"api\submit"|})]
            public void Post() { }
        }
        """;

    /// <summary>The named-argument route template after the fix.</summary>
    private const string NamedTemplateArgumentFixed = """
        using Microsoft.AspNetCore.Mvc;

        public class Endpoints
        {
            [HttpPost(template: "api/submit")]
            public void Post() { }
        }
        """;

    /// <summary>Verifies a backslash in a verbatim <c>[HttpGet]</c> template is reported and rewritten to a forward slash.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HttpGetVerbatimBackslashFixedAsync()
        => await VerifyFixAsync(HttpGetVerbatimBackslashSource, HttpGetVerbatimBackslashFixed);

    /// <summary>Verifies an escaped backslash in a <c>[Route]</c> template is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RouteEscapedBackslashFixedAsync()
        => await VerifyFixAsync(RouteEscapedBackslashSource, RouteEscapedBackslashFixed);

    /// <summary>Verifies the template passed as a named argument is still reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedTemplateArgumentFixedAsync()
        => await VerifyFixAsync(NamedTemplateArgumentSource, NamedTemplateArgumentFixed);

    /// <summary>Verifies a backslash in the route <c>Name</c> property (not the template) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BackslashInRouteNameIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class Endpoints
            {
                [Route("api/products", Name = @"legacy\name")]
                public void Get() { }
            }
            """);

    /// <summary>Verifies a route template that already uses forward slashes is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForwardSlashTemplateIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class Endpoints
            {
                [HttpGet("api/products/{id}")]
                public void Get(int id) { }
            }
            """);

    /// <summary>Verifies a tab escape in a template is not treated as a backslash separator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TabEscapeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class Endpoints
            {
                [HttpGet("api/tab\t")]
                public void Get() { }
            }
            """);

    /// <summary>Verifies the rule stays silent when the ASP.NET Core routing types are absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenRoutingTypesAbsentAsync()
        => await VerifyAsync(
            """
            public sealed class RouteAttribute : System.Attribute
            {
                public RouteAttribute(string template) { }
            }

            public class Endpoints
            {
                [Route(@"api\products")]
                public void Get() { }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies with the routing stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyRoute.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + RoutingStubs
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies with the routing stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyRoute.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + RoutingStubs,
            FixedCode = fixedSource + RoutingStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
