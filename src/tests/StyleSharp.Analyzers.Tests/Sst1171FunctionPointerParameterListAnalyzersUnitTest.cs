// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Verifysst0022 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1171FunctionPointerParameterListMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1171FunctionPointerParameterListMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1171 analyzer that requires function pointer parameter lists to be on unique lines.</summary>
public class Sst1171FunctionPointerParameterListAnalyzersUnitTest
{
    /// <summary>Verifies a function pointer parameter list with all entries on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Source = """
            unsafe class C
            {
                delegate*<int, string, void> f;
            }
            """;

        await RunAsync(Source, null);
    }

    /// <summary>Verifies a function pointer parameter list split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Source = """
            unsafe class C
            {
                delegate*{|SST1171:<
                    int, string, void>|} f;
            }
            """;

        await RunAsync(Source, null);
    }

    /// <summary>Verifies the code fix rewrites the function pointer parameter list so each entry is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Source = """
            unsafe class C
            {
                delegate*{|SST1171:<
                    int, string, void>|} f;
            }
            """;

        const string FixedSource = """
            unsafe class C
            {
                delegate*<
                    int,
                    string,
                    void> f;
            }
            """;

        await RunAsync(Source, FixedSource);
    }

    /// <summary>Runs the code fix verifier with unsafe compilation enabled.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="fixedSource">The expected source after the code fix, or <see langword="null"/> to only verify the analyzer.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string? fixedSource)
    {
        var test = new Verifysst0022.Test
        {
            TestCode = source
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
