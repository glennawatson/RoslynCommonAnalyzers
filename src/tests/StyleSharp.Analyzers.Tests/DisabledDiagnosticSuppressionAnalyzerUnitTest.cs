// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyDisabledDiagnosticSuppression = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1462DisabledDiagnosticSuppressionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1462DisabledDiagnosticSuppressionAnalyzer"/>.</summary>
public class DisabledDiagnosticSuppressionAnalyzerUnitTest
{
    /// <summary>Verifies a suppression for a disabled diagnostic is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DisabledDiagnosticSuppressionIsReportedAsync()
    {
        var test = new VerifyDisabledDiagnosticSuppression.Test
        {
            TestCode = """
                       using System.Diagnostics.CodeAnalysis;

                       [{|SST1462:SuppressMessage("Style", "SST9999:Disabled rule", Justification = "Test.")|}]
                       public sealed class C
                       {
                       }
                       """,
        };

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var options = project.CompilationOptions!.WithSpecificDiagnosticOptions(
                project.CompilationOptions.SpecificDiagnosticOptions.SetItem("SST9999", ReportDiagnostic.Suppress));
            return solution.WithProjectCompilationOptions(projectId, options);
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a suppression is reported when an <c>.editorconfig</c> is what turned the diagnostic off.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This is the way severity is actually configured — the sibling test above goes through
    /// <c>SpecificDiagnosticOptions</c>, which is the ruleset and <c>NoWarn</c> path. The two arrive at the
    /// analyzer by different routes: a <c>dotnet_diagnostic.&lt;id&gt;.severity</c> entry is a severity
    /// configuration, so the compiler routes it to the per-tree diagnostic options and never hands it back
    /// through <c>AnalyzerConfigOptionsProvider</c>. Reading it from there found nothing and reported
    /// nothing, and no test noticed, because every test configured the rule the other way.
    /// </remarks>
    [Test]
    public async Task DiagnosticDisabledByAnalyzerConfigIsReportedAsync()
    {
        var test = new VerifyDisabledDiagnosticSuppression.Test
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System.Diagnostics.CodeAnalysis;

                    [{|SST1462:SuppressMessage("Major Code Smell", "the rule:Use a testable date/time provider", Justification = "Test.")|}]
                    public sealed class C
                    {
                    }
                    """,
                },
                AnalyzerConfigFiles =
                {
                    ("/.editorconfig", """
                                       root = true

                                       [*.cs]
                                       dotnet_diagnostic.the rule.severity = none
                                       """),
                },
            },
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a suppression for an enabled diagnostic is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EnabledDiagnosticSuppressionIsCleanAsync()
        => await VerifyDisabledDiagnosticSuppression.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            [SuppressMessage("Style", "SST9999:Enabled rule", Justification = "Test.")]
            public sealed class C
            {
            }
            """);
}
