// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using VerifyUnsafe = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2289UnnecessaryUnsafeContextAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the relaxed-unsafe-context rule (SST2289).</summary>
public class Sst2289UnnecessaryUnsafeContextAnalyzerUnitTest
{
    /// <summary>Verifies a block that only takes an address is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddressOfReportedAsync()
        => await RunAsync(
            """
            public class C
            {
                public void M()
                {
                    {|SST2289:unsafe|}
                    {
                        int n = 42;
                        int* p = &n;
                    }
                }
            }
            """);

    /// <summary>Verifies a block that applies sizeof to a user-defined struct is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SizeOfReportedAsync()
        => await RunAsync(
            """
            public struct Point
            {
                public int X;
                public int Y;
            }

            public class C
            {
                public int M()
                {
                    {|SST2289:unsafe|}
                    {
                        return sizeof(Point);
                    }
                }
            }
            """);

    /// <summary>Verifies a block that pins with fixed but never dereferences is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixedWithoutDereferenceReportedAsync()
        => await RunAsync(
            """
            public class C
            {
                public void M(int[] numbers)
                {
                    {|SST2289:unsafe|}
                    {
                        fixed (int* first = numbers)
                        {
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a block that dereferences a pointer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DereferenceIsCleanAsync()
        => await RunAsync(
            """
            public class C
            {
                public int M()
                {
                    unsafe
                    {
                        int n = 42;
                        int* p = &n;
                        return *p;
                    }
                }
            }
            """);

    /// <summary>Verifies a block that indexes through a pointer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PointerIndexingIsCleanAsync()
        => await RunAsync(
            """
            public class C
            {
                public int M(int[] numbers)
                {
                    unsafe
                    {
                        fixed (int* first = numbers)
                        {
                            return first[0];
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a block that reaches a member through a pointer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PointerMemberAccessIsCleanAsync()
        => await RunAsync(
            """
            public struct Point
            {
                public int X;
            }

            public class C
            {
                public int M()
                {
                    unsafe
                    {
                        Point point = default;
                        Point* p = &point;
                        return p->X;
                    }
                }
            }
            """);

    /// <summary>Verifies a block with no pointer operations at all is left to the unnecessary-modifier rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoPointerOperationIsCleanAsync()
        => await RunAsync(
            """
            public class C
            {
                public int M()
                {
                    unsafe
                    {
                        return 1;
                    }
                }
            }
            """);

    /// <summary>Verifies nothing is reported below C# 15, where the relaxations do not apply.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BelowCSharp15IsCleanAsync()
        => await RunAsync(
            """
            public class C
            {
                public void M()
                {
                    unsafe
                    {
                        int n = 42;
                        int* p = &n;
                    }
                }
            }
            """,
            LanguageVersion.CSharp13);

    /// <summary>Runs the analyzer verifier with unsafe code allowed, at the requested language version.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="languageVersion">The language version to parse with.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var test = new VerifyUnsafe.Test
        {
            TestCode = source
        };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var parseOptions = (CSharpParseOptions)project.ParseOptions!;
            var compilationOptions = (CSharpCompilationOptions)project.CompilationOptions!;
            return solution
                .WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion))
                .WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
