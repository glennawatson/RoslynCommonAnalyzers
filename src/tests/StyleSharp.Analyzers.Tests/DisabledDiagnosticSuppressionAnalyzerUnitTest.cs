// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using VerifyDisabledDiagnosticSuppression = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1462DisabledDiagnosticSuppressionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1462DisabledDiagnosticSuppressionAnalyzer"/>.</summary>
public class DisabledDiagnosticSuppressionAnalyzerUnitTest
{
    /// <summary>The analyzer configuration document used by the verifier.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>Checks qualified names and aliases still identify disabled diagnostic suppressions.</summary>
    /// <param name="name">The written attribute name.</param>
    /// <param name="checkId">The constant check id expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.Diagnostics.CodeAnalysis.SuppressMessage", "\"XYZ1000\"")]
    [Arguments("System.Diagnostics.CodeAnalysis.SuppressMessageAttribute", "\"XYZ1000:Description\"")]
    [Arguments("Suppression", "nameof(XYZ1000)")]
    [Arguments("Diagnostics::SuppressMessage", "\"XYZ1000\"")]
    public async Task QualifiedAndAliasedSuppressionsAreReportedAsync(string name, string checkId)
    {
        var test = new VerifyDisabledDiagnosticSuppression.Test
        {
            TestCode = $$"""
                using Suppression = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;
                using Diagnostics = System.Diagnostics.CodeAnalysis;
                [{|SST1462:{{name}}("Style", {{checkId}})|}]
                class XYZ1000 {}
                """,
        };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, "root = true\n[*.cs]\ndotnet_diagnostic.XYZ1000.severity = none"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks ordinary attributes, malformed suppressions, and nonconstant check ids are ignored.</summary>
    /// <param name="source">The attributed declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("[System.Obsolete] class C {}")]
    [Arguments("[System.Obsolete(\"old\")] class C {}")]
    [Arguments("[System.Obsolete(\"old\", DiagnosticId = \"XYZ1000\")] class C {}")]
    [Arguments("[System.Obsolete(\"old\", true)] class C {}")]
    [Arguments("[SuppressMessage(\"Style\", \"XYZ1000\")] class C {}")]
    [Arguments("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", Missing)] class C {}")]
    [Arguments("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", C.Id)] class C { public static string Id = \"XYZ1000\"; }")]
    [Arguments("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", null)] class C {}")]
    [Arguments("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", \"\")] class C {}")]
    [Arguments("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", \":XYZ1000\")] class C {}")]
    [Arguments("[SuppressMessage(\"Style\", \"XYZ1000\")] class C {} class SuppressMessageAttribute : System.Attribute { public SuppressMessageAttribute(string category, string id) {} }")]
    [Arguments("using Other = System.ObsoleteAttribute; [Other(\"XYZ1000\", true)] class C {}")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonSuppressionAndInvalidCheckIdsAreCleanAsync(string source) =>
        new VerifyDisabledDiagnosticSuppression.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Checks explicit enabled severities do not make suppression attributes redundant.</summary>
    /// <param name="severity">The enabled diagnostic severity.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("warning")]
    [Arguments("error")]
    [Arguments("suggestion")]
    public async Task ConfiguredEnabledSuppressionIsCleanAsync(string severity)
    {
        var test = new VerifyDisabledDiagnosticSuppression.Test { TestCode = "[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", \"XYZ1000\")] class C {}" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, $"root = true\n[*.cs]\ndotnet_diagnostic.XYZ1000.severity = {severity}"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks a compilation without the framework suppression type is ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFrameworkSuppressionTypeIsCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("[SuppressMessage(\"Style\", \"XYZ1000\")] class C {}");
        var compilation = CSharpCompilation.Create(nameof(MissingFrameworkSuppressionTypeIsCleanAsync), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Sst1462DisabledDiagnosticSuppressionAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

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
    /// <param name="severity">The severity treated as disabled by the rule.</param>
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
    [Arguments("none")]
    [Arguments("silent")]
    public async Task DiagnosticDisabledByAnalyzerConfigIsReportedAsync(string severity)
    {
        var test = new VerifyDisabledDiagnosticSuppression.Test
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System.Diagnostics.CodeAnalysis;

                    [{|SST1462:SuppressMessage("Custom", "XYZ1000:Use a testable date/time provider", Justification = "Test.")|}]
                    public sealed class C
                    {
                    }
                    """,
                },
                AnalyzerConfigFiles =
                {
                    (EditorConfigPath, $$"""
                                       root = true

                                       [*.cs]
                                       dotnet_diagnostic.XYZ1000.severity = {{severity}}
                                       """),
                },
            },
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a suppression for an enabled diagnostic is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EnabledDiagnosticSuppressionIsCleanAsync() =>
        VerifyDisabledDiagnosticSuppression.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            [SuppressMessage("Style", "SST9999:Enabled rule", Justification = "Test.")]
            public sealed class C
            {
            }
            """);
}
