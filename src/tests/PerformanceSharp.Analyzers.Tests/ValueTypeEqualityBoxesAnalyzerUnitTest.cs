// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyEquality = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1005ValueTypeEqualityBoxesAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1005 (structs should define equality members to avoid boxing).</summary>
public class ValueTypeEqualityBoxesAnalyzerUnitTest
{
    /// <summary>Verifies a plain public struct without equality members is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainPublicStructReportedAsync()
        => await VerifyNet90Async(
            """
            public struct {|PSH1005:Point|}
            {
                public int X;
            }
            """);

    /// <summary>Verifies a readonly record struct (synthesized equality) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyRecordStructIsCleanAsync()
        => await VerifyNet90Async(
            """
            public readonly record struct Point(int X);
            """);

    /// <summary>Verifies a struct that overrides <c>Equals(object)</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructOverridingObjectEqualsIsCleanAsync()
        => await VerifyNet90Async(
            """
            public struct Point
            {
                public int X;

                public override bool Equals(object obj) => obj is Point other && other.X == X;

                public override int GetHashCode() => X;
            }
            """);

    /// <summary>Verifies a struct implementing <c>IEquatable&lt;T&gt;</c> of itself is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructImplementingSelfEquatableIsCleanAsync()
        => await VerifyNet90Async(
            """
            public struct Point : System.IEquatable<Point>
            {
                public int X;

                public bool Equals(Point other) => other.X == X;
            }
            """);

    /// <summary>Verifies a struct equatable only to a different type is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructImplementingDifferentEquatableReportedAsync()
        => await VerifyNet90Async(
            """
            public struct {|PSH1005:Meters|} : System.IEquatable<int>
            {
                public int Value;

                public bool Equals(int other) => other == Value;
            }
            """);

    /// <summary>Verifies a ref struct (which cannot box) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RefStructIsCleanAsync()
        => await VerifyNet90Async(
            """
            public ref struct Cursor
            {
                public int Position;
            }
            """);

    /// <summary>Verifies an internal struct without equality members is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalStructReportedAsync()
        => await VerifyNet90Async(
            """
            internal struct {|PSH1005:Point|}
            {
                public int X;
            }
            """);

    /// <summary>Verifies a private nested helper struct is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateNestedStructIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class Outer
            {
                private struct Inner
                {
                    public int X;
                }

                public int M()
                {
                    Inner inner = default;
                    return inner.X;
                }
            }
            """);

    /// <summary>Verifies a class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class Point
            {
                public int X;
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new VerifyEquality.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
