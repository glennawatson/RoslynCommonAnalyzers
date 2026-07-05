// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyStaticLocalFunction = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2235StaticLocalFunctionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2235StaticLocalFunctionAnalyzer"/>.</summary>
public class StaticLocalFunctionAnalyzerUnitTest
{
    /// <summary>Verifies a capture-free local function is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CaptureFreeLocalFunctionIsReportedAsync()
        => await RunAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    int {|SST2235:Twice|}(int input) => input * 2;
                    return Twice(value);
                }
            }
            """);

    /// <summary>Verifies a local function that captures an outer parameter is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CapturingLocalFunctionIsCleanAsync()
        => await RunAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    int Add(int input) => input + value;
                    return Add(1);
                }
            }
            """);

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source)
        => await new VerifyStaticLocalFunction.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        }.RunAsync(CancellationToken.None);
}
