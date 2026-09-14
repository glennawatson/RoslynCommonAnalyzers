// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndocumentedBlockReportedAsync() =>
        RunAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MissingSummaryReportedAsync() =>
        RunAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndocumentedParameterReportedAsync() =>
        RunAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndocumentedTypeParameterReportedAsync() =>
        RunAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InvalidReferenceReportedAsync() =>
        RunAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FullyDocumentedBlockIsCleanAsync() =>
        RunAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InternalContainerReportedByDefaultAsync() =>
        RunAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritDocBlockIsCleanAsync() =>
        RunAnalyzerAsync(
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

    /// <summary>Verifies empty XML elements can document receiver and type parameter names.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyXmlElementsMatchDeclaredNamesAsync() =>
        RunAnalyzerAsync(
            """
            public static class Extensions
            {
                /// <summary>Helpers.</summary>
                /// <param name="value"/>
                /// <typeparam name="T"/>
                extension<T>(T value) { }
            }
            """);

    /// <summary>Verifies nameless XML references are skipped while the receiver still requires documentation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XmlReferencesWithoutNameAreIgnoredAsync() =>
        RunAnalyzerAsync(
            """
            public static class Extensions
            {
                /// <summary>Helpers.</summary>
                /// <param/>
                /// <param unexpected="value">Missing name.</param>
                /// <typeparam/>
                extension(string {|SST1655:value|}) { }
            }
            """);

    /// <summary>Verifies type parameter references on a non-generic block are rejected.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonGenericBlockRejectsTypeParameterReferenceAsync() =>
        RunAnalyzerAsync(
            """
            public static class Extensions
            {
                /// <summary>Helpers.</summary>
                /// <param name="value"/>
                /// <typeparam name="{|SST1657:T|}"/>
                extension(string value) { }
            }
            """);

    /// <summary>Verifies ordinary members are skipped and every block in a container is checked.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultipleBlocksShareContainerScopeAsync() =>
        RunAnalyzerAsync(
            """
            public static class Extensions
            {
                public static void M() { }
                public class Nested { }
                {|SST1654:extension|}(string value) { }
                {|SST1654:extension|}(int value) { }
            }
            """);

    /// <summary>Verifies unnamed receivers do not require a param element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnnamedReceiverNeedsNoParameterDocumentationAsync() =>
        RunAnalyzerAsync(
            """
            public static class Extensions
            {
                /// <summary>Static helpers.</summary>
                extension(string) { }
            }
            """);

    /// <summary>Verifies exposed and internal documentation switches govern extension containers.</summary>
    /// <param name="accessibility">The container accessibility.</param>
    /// <param name="setting">The documentation option.</param>
    /// <param name="keyword">The extension keyword with any expected diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("public", "document_exposed_elements = true", "{|SST1654:extension|}")]
    [Arguments("public", "document_exposed_elements = false", "extension")]
    [Arguments("internal", "document_internal_elements = true", "{|SST1654:extension|}")]
    [Arguments("internal", "document_internal_elements = false", "extension")]
    [Arguments("public", "document_private_elements = true", "{|SST1654:extension|}")]
    [Arguments("public", "document_private_elements = false", "{|SST1654:extension|}")]
    [Arguments("public", "document_private_fields = true", "{|SST1654:extension|}")]
    [Arguments("public", "document_private_fields = false", "{|SST1654:extension|}")]
    [Arguments("public", "document_interfaces = all", "{|SST1654:extension|}")]
    [Arguments("public", "document_interfaces = none", "{|SST1654:extension|}")]
    public Task DocumentationOptionsControlContainerScopeAsync(string accessibility, string setting, string keyword) =>
        RunAnalyzerAsync(
            $"{accessibility} static class Extensions {{ {keyword}(string value) {{ }} }}",
            $"root = true\n[*.cs]\nstylesharp.{setting}\n");

    /// <summary>Runs the analyzer verifier with a language version that supports extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="editorConfig">An optional <c>.editorconfig</c> file body to apply, or <see langword="null"/> for none.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAnalyzerAsync(string source, string? editorConfig = null)
    {
        var test = new VerifyExtensionDoc.Test { TestCode = source };

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
        var test = new VerifyExtensionDocFix.Test { TestCode = source, FixedCode = fixedSource };

        ApplyExtensionBlockParseOptions(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Applies preview parse options to a verifier so extension blocks parse.</summary>
    /// <param name="solutionTransforms">The solution-transform collection to update.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyExtensionBlockParseOptions(List<Func<Solution, ProjectId, Solution>> solutionTransforms) =>
        solutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
}
