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
    /// <summary>Verifies write-root traversal follows storage access and rejects calls, pointers, and other expressions.</summary>
    /// <param name="expression">The target syntax.</param>
    /// <param name="expected">The storage root, when one exists.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("((point)).Inner.X", "point")]
    [Arguments("point[0].X", "point")]
    [Arguments("Other(point).X", null)]
    [Arguments("point->X", null)]
    [Arguments("this.X", null)]
    [Arguments("point!", null)]
    public async Task WriteRootFollowsOnlyStorageAccessAsync(string expression, string? expected)
    {
        var target = SyntaxFactory.ParseExpression(expression);
        var root = Sst2469DiscardedReceiverWriteAnalyzer.WriteRoot(target);
        await Assert.That(root?.Identifier.ValueText).IsEqualTo(expected);
    }

    /// <summary>Verifies every increment and decrement form writes into a by-value receiver.</summary>
    /// <param name="statement">The receiver mutation with diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("++{|SST2469:point.X|};")]
    [Arguments("--{|SST2469:point.X|};")]
    [Arguments("{|SST2469:point.X|}--;")]
    [Arguments("{|SST2469:(point).X|} += 1;")]
    [Arguments("{|SST2469:point[0]|} = 1;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReceiverMutationFormsAreReportedAsync(string statement) =>
        RunAsync($$"""
            public struct Point
            {
                public int X;
                public int this[int index] { get => X; set => X = value; }
            }
            public static class PointExtensions
            {
                extension(Point point) { public void Mutate() { {{statement}} } }
            }
            """);

    /// <summary>Verifies unary reads and writes into a returned reference do not mutate the receiver copy.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnaryReadsAndReturnedObjectWritesAreCleanAsync() =>
        RunAsync("""
            public struct Point { public int X; }
            public class Box { public int X; }
            public static class PointExtensions
            {
                private static Box Other(Point point) => new Box();
                extension(Point point)
                {
                    public int Read() => -point.X + +point.X;
                    public void MutateOther() { Other(point).X = 1; _ = Other(point)!; }
                }
            }
            """);

    /// <summary>Verifies a struct-constrained generic receiver is copied and its assignment is discarded.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task StructConstrainedReceiverAssignmentIsReportedAsync() =>
        RunAsync("static class Extensions { extension<T>(T value) where T : struct { public void Reset() { {|SST2469:value|} = default; } } }");

    /// <summary>Verifies unconstrained and reference-constrained receiver assignments are not reported.</summary>
    /// <param name="constraint">The receiver's generic constraint.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("where T : class")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReceiverWithoutValueTypeConstraintIsCleanAsync(string constraint) =>
        RunAsync($"static class Extensions {{ extension<T>(T value) {constraint} {{ public void Reset() {{ value = default; }} }} }}");

    /// <summary>Verifies a nullable struct receiver remains a copied value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NullableReceiverAssignmentIsReportedAsync() =>
        RunAsync("static class Extensions { extension(int? value) { public void Reset() { {|SST2469:value|} = null; } } }");

    /// <summary>Verifies absent receiver names, empty lists, and unresolved types are safely ignored while editing.</summary>
    /// <param name="parameters">The incomplete extension receiver.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("int")]
    [Arguments("Missing point")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task IncompleteReceiverIsCleanAsync(string parameters) =>
        new VerifyDiscardedWrite.Test
        {
            TestCode = $"static class Extensions {{ extension({parameters}) {{ public void Reset() {{ point.X = 1; }} }} }}",
            CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

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
