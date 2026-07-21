// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeAntiforgery = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1710AntiforgeryValidationDisabledAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1710 (do not disable antiforgery validation on a form).</summary>
public class AntiforgeryValidationDisabledAnalyzerUnitTest
{
    /// <summary>Inline stub of the antiforgery attribute whose <c>required: false</c> application disables validation.</summary>
    private const string AttributeStub = """

                                         namespace Microsoft.AspNetCore.Antiforgery
                                         {
                                             [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
                                             public sealed class RequireAntiforgeryTokenAttribute : System.Attribute
                                             {
                                                 public RequireAntiforgeryTokenAttribute(bool required = true)
                                                 {
                                                 }
                                             }
                                         }
                                         """;

    /// <summary>Verifies the named <c>required: false</c> form on a component class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedRequiredFalseOnClassReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Antiforgery;

            [{|SES1710:RequireAntiforgeryToken(required: false)|}]
            public class Form
            {
            }
            """);

    /// <summary>Verifies the positional <c>false</c> form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalFalseReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Antiforgery;

            [{|SES1710:RequireAntiforgeryToken(false)|}]
            public class Form
            {
            }
            """);

    /// <summary>Verifies the attribute on a method with <c>required: false</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequiredFalseOnMethodReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Antiforgery;

            public class Form
            {
                [{|SES1710:RequireAntiforgeryToken(required: false)|}]
                public void Post()
                {
                }
            }
            """);

    /// <summary>Verifies <c>required: true</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequiredTrueIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Antiforgery;

            [RequireAntiforgeryToken(required: true)]
            public class Form
            {
            }
            """);

    /// <summary>Verifies the attribute with no arguments keeps its protective default and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoArgumentsIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Antiforgery;

            [RequireAntiforgeryToken]
            public class Form
            {
            }
            """);

    /// <summary>Verifies a same-named attribute on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedAttributeOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            [RequireAntiforgeryToken(required: false)]
            public class Form
            {
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class RequireAntiforgeryTokenAttribute : System.Attribute
            {
                public RequireAntiforgeryTokenAttribute(bool required = true)
                {
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the antiforgery attribute type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenAttributeUnavailableAsync()
    {
        const string Source = """
                              [RequireAntiforgeryToken(required: false)]
                              public class Form
                              {
                              }

                              [System.AttributeUsage(System.AttributeTargets.Class)]
                              public sealed class RequireAntiforgeryTokenAttribute : System.Attribute
                              {
                                  public RequireAntiforgeryTokenAttribute(bool required = true)
                                  {
                                  }
                              }
                              """;

        var test = new AnalyzeAntiforgery.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline antiforgery-attribute stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeAntiforgery.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + AttributeStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
