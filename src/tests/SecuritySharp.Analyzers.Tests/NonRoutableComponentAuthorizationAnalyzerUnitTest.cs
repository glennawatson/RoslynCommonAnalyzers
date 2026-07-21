// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeComponentAuthorization = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1703NonRoutableComponentAuthorizationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1703 ([Authorize] on a non-routable Blazor component enforces nothing).</summary>
public class NonRoutableComponentAuthorizationAnalyzerUnitTest
{
    /// <summary>The inline stub of the Blazor authorization and component markers, matched by metadata name.</summary>
    private const string BlazorStub =
        """

        namespace Microsoft.AspNetCore.Authorization
        {
            public class AuthorizeAttribute : System.Attribute
            {
            }
        }

        namespace Microsoft.AspNetCore.Components
        {
            public abstract class ComponentBase
            {
            }

            public class LayoutComponentBase : ComponentBase
            {
            }

            public sealed class RouteAttribute : System.Attribute
            {
                public RouteAttribute(string template)
                {
                }
            }
        }
        """;

    /// <summary>Verifies [Authorize] on a non-routable component reports the attribute.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AuthorizeOnNonRoutableComponentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Components;

            [{|SES1703:Authorize|}]
            public class Widget : ComponentBase
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies the marker is matched by symbol, not written text (fully qualified spelling).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedAuthorizeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            [{|SES1703:Microsoft.AspNetCore.Authorization.AuthorizeAttribute|}]
            public class Widget : ComponentBase
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies a subclass of AuthorizeAttribute on a non-routable component is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedAuthorizeAttributeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Components;

            public sealed class AdminOnlyAttribute : AuthorizeAttribute
            {
            }

            [{|SES1703:AdminOnly|}]
            public class Widget : ComponentBase
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies a routable component ([Route] present) with [Authorize] is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RoutableComponentNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Components;

            [Authorize]
            [Route("/dashboard")]
            public class Dashboard : ComponentBase
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies an abstract component with [Authorize] is exempt.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractComponentNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Components;

            [Authorize]
            public abstract class SecuredComponentBase : ComponentBase
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies a layout component (deriving from LayoutComponentBase) with [Authorize] is exempt.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LayoutComponentNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Components;

            [Authorize]
            public class MainLayout : LayoutComponentBase
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies [Authorize] on a non-component type (matched only by symbol) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonComponentTypeNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;

            [Authorize]
            public class ReportsController
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies a component without [Authorize] is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComponentWithoutAuthorizeNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class Widget : ComponentBase
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies a component with only an unrelated attribute is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComponentWithUnrelatedAttributeNotReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            [System.Obsolete]
            public class Widget : ComponentBase
            {
            }
            """ + BlazorStub);

    /// <summary>Verifies a type name on the rule-specific exempt list is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExemptTypeBySimpleNameNotReportedAsync()
    {
        const string source =
            """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Components;

            [Authorize]
            public class Widget : ComponentBase
            {
            }
            """ + BlazorStub;

        const string editorConfig =
            """
            root = true
            [*.cs]
            securitysharp.SES1703.exempt_types = Other, Widget

            """;

        await VerifyWithConfigAsync(source, editorConfig);
    }

    /// <summary>Verifies a fully qualified type name on the project-wide exempt list is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExemptTypeByFullNameNotReportedAsync()
    {
        const string source =
            """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Components;

            namespace App
            {
                [Authorize]
                public class Secret : ComponentBase
                {
                }
            }
            """ + BlazorStub;

        const string editorConfig =
            """
            root = true
            [*.cs]
            securitysharp.exempt_types = App.Secret

            """;

        await VerifyWithConfigAsync(source, editorConfig);
    }

    /// <summary>Verifies a component absent from a populated exempt list is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonExemptTypeStillReportedAsync()
    {
        const string source =
            """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Components;

            [{|SES1703:Authorize|}]
            public class Widget : ComponentBase
            {
            }
            """ + BlazorStub;

        const string editorConfig =
            """
            root = true
            [*.cs]
            securitysharp.SES1703.exempt_types = Other

            """;

        await VerifyWithConfigAsync(source, editorConfig);
    }

    /// <summary>Verifies a project without the Blazor markers registers nothing (the rule is gated off).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBlazorReferencesNotReportedAsync()
        => await VerifyAsync(
            """
            [MyApp.Authorize]
            public class Widget
            {
            }

            namespace MyApp
            {
                public sealed class AuthorizeAttribute : System.Attribute
                {
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeComponentAuthorization.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with a supplied <c>.editorconfig</c>.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="editorConfig">The <c>.editorconfig</c> content.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithConfigAsync(string source, string editorConfig)
    {
        var test = new AnalyzeComponentAuthorization.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", editorConfig));

        await test.RunAsync(CancellationToken.None);
    }
}
