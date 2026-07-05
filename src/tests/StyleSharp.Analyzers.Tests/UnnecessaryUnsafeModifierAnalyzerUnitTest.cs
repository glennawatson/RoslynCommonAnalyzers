// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyUnnecessaryUnsafeModifier = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1455UnnecessaryUnsafeModifierAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1455UnnecessaryUnsafeModifierAnalyzer"/>.</summary>
public class UnnecessaryUnsafeModifierAnalyzerUnitTest
{
    /// <summary>Verifies an unsafe method with no unsafe syntax is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnsafeMethodWithoutUnsafeSyntaxIsReportedAsync()
        => await RunUnsafeAsync(
            """
            public sealed class C
            {
                public {|SST1455:unsafe|} void M()
                {
                    System.Console.WriteLine(1);
                }
            }
            """);

    /// <summary>Verifies pointer syntax keeps the unsafe modifier meaningful.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PointerSyntaxIsCleanAsync()
        => await RunUnsafeAsync(
            """
            public sealed class C
            {
                public unsafe int M(int* value) => *value;
            }
            """);

    /// <summary>Runs the analyzer verifier with unsafe compilation enabled.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunUnsafeAsync(string source)
    {
        var test = new VerifyUnnecessaryUnsafeModifier.Test
        {
            TestCode = source
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
