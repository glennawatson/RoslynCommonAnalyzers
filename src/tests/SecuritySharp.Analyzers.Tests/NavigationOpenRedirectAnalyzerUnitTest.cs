// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeNavigation = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1705NavigationOpenRedirectAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1705 (a Blazor component must navigate only to a verified relative URL).</summary>
public class NavigationOpenRedirectAnalyzerUnitTest
{
    /// <summary>The inline stub of the Blazor <c>NavigationManager</c> surface the rule gates on.</summary>
    private const string NavigationStub =
        """

        namespace Microsoft.AspNetCore.Components
        {
            public sealed class NavigationOptions { }

            public abstract class NavigationManager
            {
                public void NavigateTo(string uri) { }

                public void NavigateTo(string uri, bool forceLoad) { }

                public void NavigateTo(string uri, bool forceLoad, bool replace) { }

                public void NavigateTo(string uri, NavigationOptions options) { }
            }
        }
        """;

    /// <summary>Verifies a non-constant navigation target is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantTargetReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go(string url) => _nav.NavigateTo({|SES1705:url|});
            }
            """);

    /// <summary>Verifies an absolute-URL literal (which leaves the origin) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbsoluteLiteralReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go() => _nav.NavigateTo({|SES1705:"https://attacker.example/login"|});
            }
            """);

    /// <summary>Verifies a protocol-relative literal (which leaves the origin) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtocolRelativeLiteralReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go() => _nav.NavigateTo({|SES1705:"//attacker.example/login"|});
            }
            """);

    /// <summary>Verifies a backslash protocol-relative literal (a browser origin-escape trick) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BackslashProtocolRelativeLiteralReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go() => _nav.NavigateTo({|SES1705:"/\\attacker.example"|});
            }
            """);

    /// <summary>Verifies a non-constant interpolated target is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedTargetReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go(int id) => _nav.NavigateTo({|SES1705:$"/user/{id}"|});
            }
            """);

    /// <summary>Verifies the <c>forceLoad</c> overload still reports its non-constant target.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForceLoadOverloadReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go(string url) => _nav.NavigateTo({|SES1705:url|}, true);
            }
            """);

    /// <summary>Verifies a named <c>uri:</c> non-constant argument is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedUriArgumentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go(string url) => _nav.NavigateTo(forceLoad: true, uri: {|SES1705:url|});
            }
            """);

    /// <summary>Verifies a navigation on a subclass of <c>NavigationManager</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubclassNavigationReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class AppNavigationManager : NavigationManager { }

            public class C
            {
                private AppNavigationManager _nav = null!;

                public void Go(string url) => _nav.NavigateTo({|SES1705:url|});
            }
            """);

    /// <summary>Verifies a target from a non-allow-listed method call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAllowListedValidatorReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go(string url) => _nav.NavigateTo({|SES1705:Passthrough(url)|});

                private static string Passthrough(string value) => value;
            }
            """);

    /// <summary>Verifies a constant rooted-relative literal is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RootedRelativeLiteralCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go() => _nav.NavigateTo("/counter");
            }
            """);

    /// <summary>Verifies a constant base-relative literal (no leading slash) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseRelativeLiteralCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go() => _nav.NavigateTo("counter");
            }
            """);

    /// <summary>Verifies a constant tilde-relative literal is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TildeRelativeLiteralCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private NavigationManager _nav = null!;

                public void Go() => _nav.NavigateTo("~/counter");
            }
            """);

    /// <summary>Verifies a relative <c>const</c> field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RelativeConstFieldCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private const string Home = "/home";
                private NavigationManager _nav = null!;

                public void Go() => _nav.NavigateTo(Home);
            }
            """);

    /// <summary>Verifies a same-named <c>NavigateTo</c> on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedNavigateOnUnrelatedTypeCleanAsync()
        => await VerifyAsync(
            """
            public class NotANavigator
            {
                public void NavigateTo(string uri) { }

                public void Go(string url) => NavigateTo(url);
            }
            """);

    /// <summary>Verifies a target produced by an allow-listed validator is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllowListedValidatorCleanAsync()
    {
        var test = new AnalyzeNavigation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using Microsoft.AspNetCore.Components;

                       public class C
                       {
                           private NavigationManager _nav = null!;

                           public void Go(string url) => _nav.NavigateTo(EnsureLocal(url));

                           private static string EnsureLocal(string value) => value;
                       }
                       """ + NavigationStub,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1705.validators = EnsureLocal, Sanitize

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a validator named only in the project-wide key is honoured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProjectWideAllowListedValidatorCleanAsync()
    {
        var test = new AnalyzeNavigation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using Microsoft.AspNetCore.Components;

                       public class C
                       {
                           private NavigationManager _nav = null!;

                           public void Go(string url) => _nav.NavigateTo(EnsureLocal(url));

                           private static string EnsureLocal(string value) => value;
                       }
                       """ + NavigationStub,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.validators = EnsureLocal

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent when <c>NavigationManager</c> is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenNavigationManagerUnavailableAsync()
    {
        const string Source = """
                              public abstract class NavigationManager
                              {
                                  public void NavigateTo(string uri) { }
                              }

                              public class C
                              {
                                  private NavigationManager _nav = null!;

                                  public void Go(string url) => _nav.NavigateTo(url);
                              }
                              """;

        var test = new AnalyzeNavigation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline Blazor navigation stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeNavigation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + NavigationStub,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
