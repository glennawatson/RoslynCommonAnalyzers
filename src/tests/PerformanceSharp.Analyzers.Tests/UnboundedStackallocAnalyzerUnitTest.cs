// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1009UnboundedStackallocAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1009UnboundedStackallocAnalyzer"/> (PSH1009 unbounded stackalloc).</summary>
public class UnboundedStackallocAnalyzerUnitTest
{
    /// <summary>Verifies every supported constant guard and near-miss guard shape.</summary>
    /// <param name="condition">The enclosing condition.</param>
    /// <param name="bounded">Whether the current rule considers the condition a bound.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("length < 32", true)]
    [Arguments("length >= 32", true)]
    [Arguments("length > 32", true)]
    [Arguments("length == 32", true)]
    [Arguments("32 > length", true)]
    [Arguments("length != 32", false)]
    [Arguments("length < limit", false)]
    [Arguments("enabled", false)]
    [Arguments("enabled && length <= 32", true)]
    [Arguments("length <= 32 && enabled", true)]
    [Arguments("enabled || length <= 32", true)]
    [Arguments("enabled && length < limit", false)]
    [Arguments("(length <= 32)", true)]
    [Arguments("!(length > 32)", true)]
    [Arguments("!enabled", false)]
    [Arguments("length is 32", true)]
    [Arguments("length is not > 32", true)]
    [Arguments("length is (<= 32)", true)]
    [Arguments("length is int and <= 32", true)]
    [Arguments("length is int or <= 32", true)]
    [Arguments("length is int and var count", false)]
    [Arguments("length is var count", false)]
    public Task GuardShapesUseTheirCurrentBoundHeuristicAsync(string condition, bool bounded)
    {
        var allocation = bounded ? "stackalloc byte[length]" : "{|PSH1009:stackalloc byte[length]|}";
        return VerifyNet90Async($$"""
            using System;
            class C
            {
                int M(int length, int limit, bool enabled)
                {
                    if ({{condition}}) { Span<byte> buffer = {{allocation}}; return buffer.Length; }
                    return 0;
                }
            }
            """);
    }

    /// <summary>Verifies size expressions distinguish constants and trusted calls from mutable lengths.</summary>
    /// <param name="size">The allocation size expression.</param>
    /// <param name="bounded">Whether the current rule treats the expression as bounded.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Math.Clamp(length, 0, 32)", true)]
    [Arguments("Math.Max(length, 32)", false)]
    [Arguments("Min(length)", false)]
    [Arguments("C.Min(length)", true)]
    [Arguments("Mutable", false)]
    [Arguments("_readonly", false)]
    [Arguments("Limit", true)]
    public Task SizeExpressionsUseTheirCurrentBoundHeuristicAsync(string size, bool bounded)
    {
        var allocation = bounded ? $"stackalloc byte[{size}]" : $"{{|PSH1009:stackalloc byte[{size}]|}}";
        return VerifyNet90Async($$"""
            using System;
            class C
            {
                static int Mutable;
                readonly int _readonly;
                const int Limit = 32;
                static int Min(int value) => value;
                int M(int length) { Span<byte> buffer = {{allocation}}; return buffer.Length; }
            }
            """);
    }

    /// <summary>Verifies inferred array sizes and implicit stack allocations need no bound.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InitializedAllocationsAreCleanAsync() =>
        VerifyNet90Async("using System; class C { int M() { Span<int> first = stackalloc int[] { 1, 2 }; Span<int> second = stackalloc[] { 3, 4 }; return first.Length + second.Length; } }");

    /// <summary>Verifies allocations inside a condition cannot borrow that condition as a bound.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AllocationInItsOwnConditionIsReportedAsync() =>
        VerifyNet90Async(
            """
            using System;
            class C
            {
                bool Check(Span<byte> bytes) => bytes.Length > 0;
                int M(int length)
                {
                    if (Check({|PSH1009:stackalloc byte[length]|}) && length < 32) return 1;
                    return Check({|PSH1009:stackalloc byte[length]|}) && length < 32 ? 1 : 0;
                }
            }
            """);

    /// <summary>Verifies outer bounds do not cross local-function or lambda boundaries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task OuterGuardDoesNotBoundNestedFunctionsAsync() =>
        VerifyNet90Async(
            """
            using System;
            class C
            {
                int M(int length)
                {
                    if (length < 32)
                    {
                        int Local() { Span<byte> buffer = {|PSH1009:stackalloc byte[length]|}; return buffer.Length; }
                        Func<int> lambda = () => { Span<byte> buffer = {|PSH1009:stackalloc byte[length]|}; return buffer.Length; };
                        return Local() + lambda();
                    }
                    return 0;
                }
            }
            """);

    /// <summary>Verifies nonconstant ternary conditions do not bound either allocation arm.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnboundedConditionalArmsAreReportedAsync() =>
        VerifyNet90Async(
            """
            using System;
            class C
            {
                int M(int length, bool enabled)
                {
                    Span<byte> bytes = enabled ? {|PSH1009:stackalloc byte[length]|} : {|PSH1009:stackalloc byte[length]|};
                    return bytes.Length;
                }
            }
            """);

    /// <summary>Verifies malformed multidimensional allocations are ignored before bound analysis.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public Task MultidimensionalAllocationIsIgnoredAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = "class C { unsafe void M(int length) { int* buffer = stackalloc int[length, length]; } }",
        };
        return test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies non-array allocation types exit before requesting semantic size information.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonArrayAllocationTypeIsIgnoredAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C { unsafe void M() { int* buffer = stackalloc int[1]; } }");
        var allocation = root.DescendantNodes().OfType<StackAllocArrayCreationExpressionSyntax>().Single();
        var type = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));
        var tree = CSharpSyntaxTree.Create(root.ReplaceNode(allocation, allocation.WithType(type)));
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var diagnostics = await compilation.WithAnalyzers([new Psh1009UnboundedStackallocAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a data-driven length with no bound is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnboundedLengthIsFlaggedAsync() =>
        VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    Span<byte> buffer = {|PSH1009:stackalloc byte[length]|};
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Verifies a constant length is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstantLengthIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M()
                {
                    Span<byte> buffer = stackalloc byte[256];
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Verifies the guarded conditional spill shape is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GuardedConditionalIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    Span<char> buffer = length <= 512 ? stackalloc char[length] : new char[length];
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Verifies an enclosing if guard with a constant comparison is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EnclosingIfGuardIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    if (length <= 512)
                    {
                        Span<byte> buffer = stackalloc byte[length];
                        return buffer.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a relational pattern guard is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RelationalPatternGuardIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    if (length is > 0 and <= 512)
                    {
                        Span<byte> buffer = stackalloc byte[length];
                        return buffer.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a Math.Min clamped length is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MinClampedLengthIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    Span<char> buffer = stackalloc char[Math.Min(length, 64)];
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Verifies a static readonly threshold length is treated as bounded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticReadonlyLengthIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;

            public class C
            {
                private static readonly int Threshold = 512;

                public int M()
                {
                    Span<byte> buffer = stackalloc byte[Threshold];
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, };
        await test.RunAsync(CancellationToken.None);
    }
}
