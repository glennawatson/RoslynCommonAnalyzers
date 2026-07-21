// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeInteractiveHttpContext = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1704InteractiveComponentHttpContextAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1704 (HttpContext captured by an interactive Blazor component is stale).</summary>
public class InteractiveComponentHttpContextAnalyzerUnitTest
{
    /// <summary>The inline stub of the Blazor component, render-mode, and HTTP markers, matched by metadata name.</summary>
    private const string BlazorStub =
        """

        namespace Microsoft.AspNetCore.Components
        {
            public abstract class ComponentBase
            {
            }

            public abstract class RenderModeAttribute : System.Attribute
            {
            }

            public sealed class InjectAttribute : System.Attribute
            {
            }

            public class CascadingParameterAttribute : System.Attribute
            {
            }
        }

        namespace Microsoft.AspNetCore.Components.Web
        {
            // Mirrors the RenderModeAttribute subclass the compiler emits for an '@rendermode' on a component.
            public sealed class InteractiveServerRenderModeAttribute : Microsoft.AspNetCore.Components.RenderModeAttribute
            {
            }

            public sealed class InteractiveWebAssemblyRenderModeAttribute : Microsoft.AspNetCore.Components.RenderModeAttribute
            {
            }

            public sealed class InteractiveAutoRenderModeAttribute : Microsoft.AspNetCore.Components.RenderModeAttribute
            {
            }
        }

        namespace Microsoft.AspNetCore.Http
        {
            public interface IHttpContextAccessor
            {
            }

            public class HttpContext
            {
            }
        }
        """;

    /// <summary>Verifies an injected IHttpContextAccessor property on an interactive component is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InjectedAccessorReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.AspNetCore.Http;

            [InteractiveServerRenderMode]
            public class Dashboard : ComponentBase
            {
                [Inject]
                public IHttpContextAccessor {|SES1704:Accessor|} { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies a cascaded HttpContext parameter on an interactive component is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CascadedHttpContextReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.AspNetCore.Http;

            [InteractiveServerRenderMode]
            public class Dashboard : ComponentBase
            {
                [CascadingParameter]
                public HttpContext {|SES1704:Context|} { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies a constructor-injected IHttpContextAccessor on an interactive component is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorInjectedAccessorReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.AspNetCore.Http;

            [InteractiveServerRenderMode]
            public class Dashboard : ComponentBase
            {
                public Dashboard(string title, IHttpContextAccessor {|SES1704:accessor|})
                {
                }
            }
            """ + BlazorStub);

    /// <summary>Verifies an injected IHttpContextAccessor field on an interactive component is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InjectedAccessorFieldReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.AspNetCore.Http;

            [InteractiveServerRenderMode]
            public class Dashboard : ComponentBase
            {
                [Inject]
                public IHttpContextAccessor {|SES1704:Accessor|};
            }
            """ + BlazorStub);

    /// <summary>Verifies the Interactive WebAssembly render mode is treated as interactive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WebAssemblyRenderModeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.AspNetCore.Http;

            [InteractiveWebAssemblyRenderMode]
            public class Dashboard : ComponentBase
            {
                [Inject]
                public IHttpContextAccessor {|SES1704:Accessor|} { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies the Interactive Auto render mode is treated as interactive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AutoRenderModeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.AspNetCore.Http;

            [InteractiveAutoRenderMode]
            public class Dashboard : ComponentBase
            {
                [CascadingParameter]
                public HttpContext {|SES1704:Context|} { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies a custom attribute deriving from a render-mode attribute is treated as interactive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedRenderModeAttributeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Http;

            public sealed class AppRenderModeAttribute : Microsoft.AspNetCore.Components.RenderModeAttribute
            {
            }

            [AppRenderMode]
            public class Dashboard : ComponentBase
            {
                [Inject]
                public IHttpContextAccessor {|SES1704:Accessor|} { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies a component with no render mode (static server rendering) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticComponentNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Http;

            public class Dashboard : ComponentBase
            {
                [Inject]
                public IHttpContextAccessor Accessor { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies an interactive component injecting an unrelated service is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InteractiveComponentWithoutHttpContextNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;

            [InteractiveServerRenderMode]
            public class Dashboard : ComponentBase
            {
                [Inject]
                public SomeService Service { get; set; }
            }

            public class SomeService
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies a cascaded non-HttpContext parameter on an interactive component is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CascadedNonHttpContextNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;

            [InteractiveServerRenderMode]
            public class Dashboard : ComponentBase
            {
                [CascadingParameter]
                public string Theme { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies a non-component type carrying a render-mode attribute and an accessor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonComponentWithRenderModeNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.AspNetCore.Http;

            [InteractiveServerRenderMode]
            public class NotAComponent
            {
                [Inject]
                public IHttpContextAccessor Accessor { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies a component whose only attribute is not a render mode is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComponentWithNonRenderModeAttributeNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Http;

            [System.Obsolete]
            public class Dashboard : ComponentBase
            {
                [Inject]
                public IHttpContextAccessor Accessor { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies plain and unrelated-attributed members on an interactive component are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InteractiveComponentWithPlainMembersNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Web;

            [InteractiveServerRenderMode]
            public class Dashboard : ComponentBase
            {
                public int Count { get; set; }

                public string Name;

                [System.Obsolete]
                public string Title { get; set; }
            }
            """ + BlazorStub);

    /// <summary>Verifies a project without the Blazor markers registers nothing (the rule is gated off).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBlazorReferencesNotReportedAsync()
        => await VerifyAsync(
            """
            public class Dashboard
            {
                public int Value { get; set; }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeInteractiveHttpContext.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
