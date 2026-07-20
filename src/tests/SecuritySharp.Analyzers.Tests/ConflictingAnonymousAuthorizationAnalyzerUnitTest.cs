// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeAuthorization = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1507ConflictingAnonymousAuthorizationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1507 (a declaration must not carry both [AllowAnonymous] and [Authorize]).</summary>
public class ConflictingAnonymousAuthorizationAnalyzerUnitTest
{
    /// <summary>The inline stub of the ASP.NET Core authorization markers, matched by metadata name.</summary>
    private const string AuthorizationStub =
        """

        namespace Microsoft.AspNetCore.Authorization
        {
            public class AuthorizeAttribute : System.Attribute
            {
            }

            public sealed class AllowAnonymousAttribute : System.Attribute
            {
            }
        }
        """;

    /// <summary>Verifies both markers on a type report the [Authorize] attribute.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BothMarkersOnTypeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;

            [{|SES1507:Authorize|}]
            [AllowAnonymous]
            public class HomeController
            {
            }
            """ + AuthorizationStub);

    /// <summary>Verifies both markers on a method report the [Authorize] attribute.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BothMarkersOnMethodReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;

            public class HomeController
            {
                [{|SES1507:Authorize|}]
                [AllowAnonymous]
                public void Index()
                {
                }
            }
            """ + AuthorizationStub);

    /// <summary>Verifies both markers written in one attribute list are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BothMarkersInOneAttributeListReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;

            [{|SES1507:Authorize|}, AllowAnonymous]
            public class HomeController
            {
            }
            """ + AuthorizationStub);

    /// <summary>Verifies the markers are matched by symbol, not written text (fully qualified spelling).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedMarkersReportedAsync()
        => await VerifyAsync(
            """
            [{|SES1507:Microsoft.AspNetCore.Authorization.AuthorizeAttribute|}]
            [Microsoft.AspNetCore.Authorization.AllowAnonymous]
            public class HomeController
            {
            }
            """ + AuthorizationStub);

    /// <summary>Verifies a subclass of AuthorizeAttribute alongside [AllowAnonymous] is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedAuthorizeAttributeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;

            public sealed class AdminOnlyAttribute : AuthorizeAttribute
            {
            }

            public class HomeController
            {
                [{|SES1507:AdminOnly|}]
                [AllowAnonymous]
                public void Index()
                {
                }
            }
            """ + AuthorizationStub);

    /// <summary>Verifies [Authorize] alone (with an unrelated attribute) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AuthorizeWithoutAllowAnonymousIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;

            [Authorize]
            [System.Obsolete]
            public class HomeController
            {
            }
            """ + AuthorizationStub);

    /// <summary>Verifies [AllowAnonymous] alone (with an unrelated attribute) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllowAnonymousWithoutAuthorizeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;

            [AllowAnonymous]
            [System.Obsolete]
            public class HomeController
            {
            }
            """ + AuthorizationStub);

    /// <summary>Verifies an [Authorize] type with a separate [AllowAnonymous] member is not reported (intended pattern).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeAuthorizeWithMemberAllowAnonymousIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authorization;

            [Authorize]
            [System.Obsolete]
            public class HomeController
            {
                [AllowAnonymous]
                [System.Obsolete]
                public void Public()
                {
                }
            }
            """ + AuthorizationStub);

    /// <summary>Verifies two unrelated attributes on a declaration are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedAttributesAreCleanAsync()
        => await VerifyAsync(
            """
            [System.Serializable]
            [System.Obsolete]
            public class HomeController
            {
            }
            """ + AuthorizationStub);

    /// <summary>Verifies markers from a different namespace are not reported (matched by metadata name, and the rule is gated on the ASP.NET Core type).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MarkersFromDifferentNamespaceAreCleanAsync()
        => await VerifyAsync(
            """
            [MyApp.Authorize]
            [MyApp.AllowAnonymous]
            public class HomeController
            {
            }

            namespace MyApp
            {
                public sealed class AuthorizeAttribute : System.Attribute
                {
                }

                public sealed class AllowAnonymousAttribute : System.Attribute
                {
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeAuthorization.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
