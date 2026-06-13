// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyExtensionDoc = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ExtensionBlockDocumentationAnalyzer>;
using VerifyExtensionDocFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExtensionBlockDocumentationAnalyzer,
    StyleSharp.Analyzers.DocumentationStubCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the extension-block documentation rules (SST1654–SST1657).</summary>
public class ExtensionBlockDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies an extension block with no documentation comment is reported (SST1654).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UndocumentedBlockReportedAsync()
        => await RunAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                {|SST1654:extension|}(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies a documented block that omits the summary is reported (SST1654).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingSummaryReportedAsync()
        => await RunAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                /// <param name="text">The text.</param>
                {|SST1654:extension|}(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies an undocumented receiver parameter is reported (SST1655).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UndocumentedParameterReportedAsync()
        => await RunAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                /// <summary>Adds helpers.</summary>
                extension(string {|SST1655:text|})
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies an undocumented type parameter is reported (SST1656).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UndocumentedTypeParameterReportedAsync()
        => await RunAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                /// <summary>Adds helpers.</summary>
                /// <param name="value">The value.</param>
                extension<{|SST1656:T|}>(T value)
                {
                    public bool IsDefault => value is null;
                }
            }
            """);

    /// <summary>Verifies stray parameter and type-parameter references are reported (SST1657).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidReferenceReportedAsync()
        => await RunAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                /// <summary>Adds helpers.</summary>
                /// <typeparam name="T">The element type.</typeparam>
                /// <typeparam name="{|SST1657:TWrong|}">Not real.</typeparam>
                /// <param name="value">The value.</param>
                /// <param name="{|SST1657:other|}">Not real.</param>
                extension<T>(T value)
                {
                    public bool IsDefault => value is null;
                }
            }
            """);

    /// <summary>Verifies a fully documented extension block produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyDocumentedBlockIsCleanAsync()
        => await RunAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                /// <summary>Adds helpers to a value.</summary>
                /// <typeparam name="T">The element type.</typeparam>
                /// <param name="value">The value.</param>
                extension<T>(T value)
                {
                    public bool IsDefault => value is null;
                }
            }
            """);

    /// <summary>Verifies an extension block in an internal container is reported by default (internal elements are documented by default).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalContainerReportedByDefaultAsync()
        => await RunAnalyzerAsync(
            """
            internal static class SampleExtensions
            {
                {|SST1654:extension|}(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies setting document_internal_elements = false stops an internal container's block from being reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalContainerIgnoredWhenInternalDisabledAsync()
    {
        const string Source = """
            internal static class SampleExtensions
            {
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """;
        const string EditorConfig = """
            root = true
            [*.cs]
            stylesharp.document_internal_elements = false

            """;

        await RunAnalyzerAsync(Source, EditorConfig);
    }

    /// <summary>Verifies a block that inherits its documentation is ignored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocBlockIsCleanAsync()
        => await RunAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                /// <inheritdoc/>
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies the code fix inserts a <c>&lt;param&gt;</c> stub for an undocumented parameter (SST1655).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterStubInsertedAsync()
    {
        const string Source = """
            public static class SampleExtensions
            {
                /// <summary>Adds helpers.</summary>
                extension(string {|SST1655:text|})
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """;
        const string FixedSource = """
            public static class SampleExtensions
            {
                /// <summary>Adds helpers.</summary>
                /// <param name="text"></param>
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """;
        await RunCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the code fix inserts a <c>&lt;typeparam&gt;</c> stub for an undocumented type parameter (SST1656).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterStubInsertedAsync()
    {
        const string Source = """
            public static class SampleExtensions
            {
                /// <summary>Adds helpers.</summary>
                /// <param name="value">The value.</param>
                extension<{|SST1656:T|}>(T value)
                {
                    public bool IsDefault => value is null;
                }
            }
            """;
        const string FixedSource = """
            public static class SampleExtensions
            {
                /// <summary>Adds helpers.</summary>
                /// <param name="value">The value.</param>
                /// <typeparam name="T"></typeparam>
                extension<T>(T value)
                {
                    public bool IsDefault => value is null;
                }
            }
            """;
        await RunCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Runs the analyzer verifier with a language version that supports extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="editorConfig">An optional <c>.editorconfig</c> file body to apply, or <see langword="null"/> for none.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAnalyzerAsync(string source, string? editorConfig = null)
    {
        var test = new VerifyExtensionDoc.Test
        {
            TestCode = source
        };

        if (editorConfig is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", editorConfig));
        }

        ApplyExtensionBlockParseOptions(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the code-fix verifier with a language version that supports extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="fixedSource">The expected fixed code.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunCodeFixAsync(string source, string fixedSource)
    {
        var test = new VerifyExtensionDocFix.Test
        {
            TestCode = source,
            FixedCode = fixedSource
        };

        ApplyExtensionBlockParseOptions(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Applies preview parse options to a verifier so extension blocks parse.</summary>
    /// <param name="solutionTransforms">The solution-transform collection to update.</param>
    private static void ApplyExtensionBlockParseOptions(List<Func<Solution, ProjectId, Solution>> solutionTransforms)
        => solutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
}
