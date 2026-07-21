// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyApiAction = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2704ApiActionMissingHttpVerbAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2704 (an API controller action should declare an HTTP verb).</summary>
public class ApiActionMissingHttpVerbAnalyzerUnitTest
{
    /// <summary>The inline stubs of the ASP.NET Core MVC controller and routing surface the rule gates on.</summary>
    private const string MvcStubs = """

        namespace Microsoft.AspNetCore.Mvc.Routing
        {
            public interface IActionHttpMethodProvider { }

            public abstract class HttpMethodAttribute : System.Attribute, IActionHttpMethodProvider
            {
                protected HttpMethodAttribute(System.Collections.Generic.IEnumerable<string> httpMethods) { }
            }
        }

        namespace Microsoft.AspNetCore.Mvc
        {
            public sealed class ApiControllerAttribute : System.Attribute { }

            public sealed class NonActionAttribute : System.Attribute { }

            public sealed class RouteAttribute : System.Attribute
            {
                public RouteAttribute(string template) { }
            }

            public sealed class HttpGetAttribute : Routing.HttpMethodAttribute
            {
                public HttpGetAttribute() : base(System.Array.Empty<string>()) { }

                public HttpGetAttribute(string template) : base(System.Array.Empty<string>()) { }
            }

            public sealed class AcceptVerbsAttribute : System.Attribute, Routing.IActionHttpMethodProvider
            {
                public AcceptVerbsAttribute(string method) { }
            }

            public abstract class ControllerBase { }
        }
        """;

    /// <summary>Verifies a public action with no verb attribute on an [ApiController] type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerblessActionReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            public class WidgetController : ControllerBase
            {
                public object {|SST2704:List|}() => new object();
            }
            """);

    /// <summary>Verifies a method that only carries <c>[Route]</c> (no verb) is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RouteWithoutVerbReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            public class WidgetController : ControllerBase
            {
                [Route("widgets")]
                public object {|SST2704:List|}() => new object();
            }
            """);

    /// <summary>Verifies an action with a verb attribute is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ActionWithVerbIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            public class WidgetController : ControllerBase
            {
                [HttpGet]
                public object List() => new object();
            }
            """);

    /// <summary>Verifies an attribute that supplies verbs through the provider interface exempts the action.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AcceptVerbsActionIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            public class WidgetController : ControllerBase
            {
                [AcceptVerbs("GET")]
                public object List() => new object();
            }
            """);

    /// <summary>Verifies a method marked <c>[NonAction]</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonActionMethodIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            public class WidgetController : ControllerBase
            {
                [NonAction]
                public object Helper() => new object();
            }
            """);

    /// <summary>Verifies a static method, a property, and an <c>object</c> override are not treated as actions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonActionShapesAreCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            public class WidgetController : ControllerBase
            {
                public static object Create() => new object();

                public int Count { get; set; }

                public override string ToString() => "widget";

                private object Hidden() => new object();
            }
            """);

    /// <summary>Verifies a controller without <c>[ApiController]</c> is out of scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonApiControllerIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class WidgetController : ControllerBase
            {
                public object List() => new object();
            }
            """);

    /// <summary>Verifies the rule stays silent when the ASP.NET Core MVC types are absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenMvcTypesAbsentAsync()
        => await VerifyAsync(
            """
            public sealed class ApiControllerAttribute : System.Attribute { }

            public abstract class ControllerBase { }

            [ApiController]
            public class WidgetController : ControllerBase
            {
                public object List() => new object();
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies with the MVC stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyApiAction.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + MvcStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
