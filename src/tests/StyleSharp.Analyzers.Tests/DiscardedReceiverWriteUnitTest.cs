// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;

using VerifyDiscardedWrite = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2469DiscardedReceiverWriteAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the discarded struct-receiver write rule (SST2469).</summary>
public class DiscardedReceiverWriteUnitTest
{
    /// <summary>Verifies a setter writing into a by-value struct receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SetterWritingToAByValueStructReceiverIsReportedAsync() =>
        RunAsync(
            """
            public struct Point
            {
                public int X { get; set; }
            }

            public static class PointExtensions
            {
                extension(Point point)
                {
                    public int Doubled
                    {
                        get => point.X * 2;
                        set => {|SST2469:point.X|} = value / 2;
                    }
                }
            }
            """);

    /// <summary>Verifies an increment of a by-value struct receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IncrementOfAByValueStructReceiverIsReportedAsync() =>
        RunAsync(
            """
            public struct Point
            {
                public int X { get; set; }
            }

            public static class PointExtensions
            {
                extension(Point point)
                {
                    public void Bump() => {|SST2469:point.X|}++;
                }
            }
            """);

    /// <summary>Verifies a ref receiver is not reported, because the write reaches the caller.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RefStructReceiverIsCleanAsync() =>
        RunAsync(
            """
            public struct Point
            {
                public int X { get; set; }
            }

            public static class PointExtensions
            {
                extension(ref Point point)
                {
                    public int Doubled
                    {
                        get => point.X * 2;
                        set => point.X = value / 2;
                    }
                }
            }
            """);

    /// <summary>Verifies a class receiver is not reported, because the write reaches the same object.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassReceiverIsCleanAsync() =>
        RunAsync(
            """
            public class Box
            {
                public int X { get; set; }
            }

            public static class BoxExtensions
            {
                extension(Box box)
                {
                    public int Doubled
                    {
                        get => box.X * 2;
                        set => box.X = value / 2;
                    }
                }
            }
            """);

    /// <summary>Verifies reading a by-value struct receiver is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadOfAByValueStructReceiverIsCleanAsync() =>
        RunAsync(
            """
            public struct Point
            {
                public int X { get; set; }
            }

            public static class PointExtensions
            {
                extension(Point point)
                {
                    public int Doubled => point.X * 2;
                }
            }
            """);

    /// <summary>Verifies a write to a local rather than the receiver is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WriteToALocalCopyIsCleanAsync() =>
        RunAsync(
            """
            public struct Point
            {
                public int X { get; set; }
            }

            public static class PointExtensions
            {
                extension(Point point)
                {
                    public int Moved
                    {
                        get
                        {
                            var copy = point;
                            copy.X = 5;
                            return copy.X;
                        }
                    }
                }
            }
            """);

    /// <summary>Runs the verifier at a language version that has extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source)
    {
        var test = new VerifyDiscardedWrite.Test { TestCode = source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
