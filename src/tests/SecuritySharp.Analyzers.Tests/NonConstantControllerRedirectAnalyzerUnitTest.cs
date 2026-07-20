// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeRedirect = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1510NonConstantControllerRedirectAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1510 (an MVC controller must not redirect to a non-constant URL).</summary>
public class NonConstantControllerRedirectAnalyzerUnitTest
{
    /// <summary>The inline stub of the ASP.NET Core controller redirect surface the rule gates on.</summary>
    private const string ControllerStub =
        """

        namespace Microsoft.AspNetCore.Mvc
        {
            public class RedirectResult { }

            public class LocalRedirectResult { }

            public class RedirectToActionResult { }

            public abstract class ControllerBase
            {
                public virtual RedirectResult Redirect(string url) => new RedirectResult();

                public virtual RedirectResult RedirectPermanent(string url) => new RedirectResult();

                public virtual RedirectResult RedirectPreserveMethod(string url) => new RedirectResult();

                public virtual RedirectResult RedirectPermanentPreserveMethod(string url) => new RedirectResult();

                public virtual LocalRedirectResult LocalRedirect(string localUrl) => new LocalRedirectResult();

                public virtual RedirectToActionResult RedirectToAction(string actionName) => new RedirectToActionResult();
            }
        }
        """;

    /// <summary>Verifies a non-constant <c>Redirect</c> target on a controller is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectNonConstantReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go(string url) => Redirect({|SES1510:url|});
            }
            """);

    /// <summary>Verifies a non-constant <c>RedirectPermanent</c> target is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectPermanentNonConstantReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go(string url) => RedirectPermanent({|SES1510:url|});
            }
            """);

    /// <summary>Verifies a non-constant <c>RedirectPreserveMethod</c> target is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectPreserveMethodNonConstantReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go(string url) => RedirectPreserveMethod({|SES1510:url|});
            }
            """);

    /// <summary>Verifies a non-constant <c>RedirectPermanentPreserveMethod</c> target is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectPermanentPreserveMethodNonConstantReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go(string url) => RedirectPermanentPreserveMethod({|SES1510:url|});
            }
            """);

    /// <summary>Verifies the explicit <c>this.Redirect(...)</c> member-access form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectThroughThisReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go(string url) => this.Redirect({|SES1510:url|});
            }
            """);

    /// <summary>Verifies a non-constant interpolated URL is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectInterpolatedTargetReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go(int id) => Redirect({|SES1510:$"/user/{id}"|});
            }
            """);

    /// <summary>Verifies a redirect to a property-backed target is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectToPropertyTargetReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public string Target { get; set; } = "/";

                public object Go() => Redirect({|SES1510:Target|});
            }
            """);

    /// <summary>Verifies a redirect on a subclass of a controller is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectOnControllerSubclassReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public abstract class AppControllerBase : ControllerBase
            {
            }

            public class HomeController : AppControllerBase
            {
                public object Go(string url) => Redirect({|SES1510:url|});
            }
            """);

    /// <summary>Verifies a hard-coded literal URL is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectConstantLiteralIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go() => Redirect("/home");
            }
            """);

    /// <summary>Verifies a redirect to a <c>const</c> field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectConstFieldIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                private const string HomeUrl = "/home";

                public object Go() => Redirect(HomeUrl);
            }
            """);

    /// <summary>Verifies <c>LocalRedirect</c> (already local-only) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalRedirectIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go(string url) => LocalRedirect(url);
            }
            """);

    /// <summary>Verifies <c>RedirectToAction</c> (an action name, not a URL) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedirectToActionIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public object Go(string actionName) => RedirectToAction(actionName);
            }
            """);

    /// <summary>Verifies a same-named <c>Redirect</c> on an unrelated (non-controller) type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedRedirectOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public class NotAController
            {
                public object Redirect(string url) => null;

                public object Go(string url) => Redirect(url);
            }
            """);

    /// <summary>Verifies the rule stays silent when the controller surface is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenControllerBaseUnavailableAsync()
    {
        const string Source = """
                              public abstract class ControllerBase
                              {
                                  public object Redirect(string url) => null;
                              }

                              public class HomeController : ControllerBase
                              {
                                  public object Go(string url) => Redirect(url);
                              }
                              """;

        var test = new AnalyzeRedirect.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core controller stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeRedirect.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + ControllerStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
