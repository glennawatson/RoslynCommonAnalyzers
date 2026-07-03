// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyInParameter = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1003InParameterWithNonReadonlyStructAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1003 ('in' parameters should use readonly structs).</summary>
public class InParameterWithNonReadonlyStructAnalyzerUnitTest
{
    /// <summary>Verifies an <c>in</c> parameter of a mutable struct is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InParameterOfMutableStructReportedAsync()
        => await VerifyNet90Async(
            """
            public struct Point
            {
                public int X;
            }

            public static class C
            {
                public static int M(in Point {|PSH1003:point|}) => point.X;
            }
            """);

    /// <summary>Verifies an <c>in</c> parameter of a readonly struct is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InParameterOfReadonlyStructIsCleanAsync()
        => await VerifyNet90Async(
            """
            public readonly struct Point
            {
                public readonly int X;
            }

            public static class C
            {
                public static int M(in Point point) => point.X;
            }
            """);

    /// <summary>Verifies an <c>in</c> parameter of a primitive type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InParameterOfPrimitiveIsCleanAsync()
        => await VerifyNet90Async(
            """
            public static class C
            {
                public static int M(in int value) => value;
            }
            """);

    /// <summary>Verifies a C# 12 <c>ref readonly</c> parameter of a mutable struct is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RefReadonlyParameterOfMutableStructReportedAsync()
        => await VerifyNet90Async(
            """
            public struct Point
            {
                public int X;
            }

            public static class C
            {
                public static int M(ref readonly Point {|PSH1003:point|}) => point.X;
            }
            """);

    /// <summary>Verifies a by-value parameter of a mutable struct is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByValueMutableStructParameterIsCleanAsync()
        => await VerifyNet90Async(
            """
            public struct Point
            {
                public int X;
            }

            public static class C
            {
                public static int M(Point point) => point.X;
            }
            """);

    /// <summary>Verifies an <c>in</c> parameter of an enum type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InParameterOfEnumIsCleanAsync()
        => await VerifyNet90Async(
            """
            public enum Color
            {
                Red
            }

            public static class C
            {
                public static Color M(in Color color) => color;
            }
            """);

    /// <summary>Verifies an <c>in</c> parameter of a reference type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InParameterOfReferenceTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            public static class C
            {
                public static int M(in string value) => value.Length;
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new VerifyInParameter.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
