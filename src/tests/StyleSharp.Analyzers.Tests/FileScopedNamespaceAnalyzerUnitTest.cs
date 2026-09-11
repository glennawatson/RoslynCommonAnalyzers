// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using VerifyFileScopedNamespace = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2237FileScopedNamespaceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2237FileScopedNamespaceAnalyzer"/>.</summary>
public class FileScopedNamespaceAnalyzerUnitTest
{
    /// <summary>The editorconfig body that asks for block-scoped namespaces.</summary>
    private const string BlockScopedConfig = """
                                             root = true
                                             [*.cs]
                                             stylesharp.namespace_declaration_style = block_scoped

                                             """;

    /// <summary>The editorconfig body whose rule-specific key contradicts its project-wide key.</summary>
    private const string RuleSpecificOverrideConfig = """
                                                      root = true
                                                      [*.cs]
                                                      stylesharp.namespace_declaration_style = block_scoped
                                                      stylesharp.SST2237.namespace_declaration_style = file_scoped

                                                      """;

    /// <summary>The editorconfig body carrying a value the rule does not recognize.</summary>
    private const string UnrecognizedStyleConfig = """
                                                   root = true
                                                   [*.cs]
                                                   stylesharp.namespace_declaration_style = sideways

                                                   """;

    /// <summary>The block-scoped namespace source whose declaration the rule reports.</summary>
    private const string ReportedBlockScopedNamespaceSource = """
        namespace {|SST2237:Bench|}
        {
            public sealed class C
            {
            }
        }
        """;

    /// <summary>Verifies a single block-scoped namespace is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleBlockScopedNamespaceIsReportedAsync() =>
        RunAsync(ReportedBlockScopedNamespaceSource);

    /// <summary>Verifies files with multiple namespace members are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultipleNamespacesAreCleanAsync() =>
        RunAsync(
            """
            namespace A
            {
                public sealed class C
                {
                }
            }

            namespace B
            {
                public sealed class D
                {
                }
            }
            """);

    /// <summary>Verifies a file-scoped namespace is clean under the default style.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FileScopedNamespaceIsCleanByDefaultAsync() =>
        RunAsync(
            """
            namespace Bench;

            public sealed class C
            {
            }
            """);

    /// <summary>Verifies a file-scoped namespace is reported once the block-scoped form is configured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FileScopedNamespaceIsReportedWhenBlockScopedIsConfiguredAsync() =>
        RunAsync(
            """
            namespace {|SST2237:Bench|};

            public sealed class C
            {
            }
            """,
            BlockScopedConfig);

    /// <summary>Verifies a block-scoped namespace is clean once the block-scoped form is configured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BlockScopedNamespaceIsCleanWhenBlockScopedIsConfiguredAsync() =>
        RunAsync(
            """
            namespace Bench
            {
                public sealed class C
                {
                }
            }
            """,
            BlockScopedConfig);

    /// <summary>Verifies the rule-specific key overrides the project-wide one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RuleSpecificKeyOverridesTheProjectWideKeyAsync() =>
        RunAsync(
            ReportedBlockScopedNamespaceSource,
            RuleSpecificOverrideConfig);

    /// <summary>Verifies an unrecognized value falls back to the documented default.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrecognizedStyleFallsBackToFileScopedAsync() =>
        RunAsync(
            ReportedBlockScopedNamespaceSource,
            UnrecognizedStyleConfig);

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <param name="editorConfig">The optional editorconfig body to apply.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source, string? editorConfig = null)
    {
        var test = new VerifyFileScopedNamespace.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source };

        if (editorConfig is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", editorConfig));
        }

        await test.RunAsync(CancellationToken.None);
    }
}
