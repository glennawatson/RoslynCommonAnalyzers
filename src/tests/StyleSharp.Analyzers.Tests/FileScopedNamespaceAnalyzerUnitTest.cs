// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyFileScopedNamespace = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2237FileScopedNamespaceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2237FileScopedNamespaceAnalyzer"/>.</summary>
public class FileScopedNamespaceAnalyzerUnitTest
{
    /// <summary>Verifies a single block-scoped namespace is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SingleBlockScopedNamespaceIsReportedAsync()
        => await RunAsync(
            """
            namespace {|SST2237:Bench|}
            {
                public sealed class C
                {
                }
            }
            """);

    /// <summary>Verifies files with multiple namespace members are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MultipleNamespacesAreCleanAsync()
        => await RunAsync(
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

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source)
        => await new VerifyFileScopedNamespace.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        }.RunAsync(CancellationToken.None);
}
