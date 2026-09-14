// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1015BoxingRoundTripCastAnalyzer,
    PerformanceSharp.Analyzers.Psh1015BoxingRoundTripCastCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1015BoxingRoundTripCastAnalyzer"/> (PSH1015 boxing round-trip casts).</summary>
public class BoxingRoundTripCastAnalyzerUnitTest
{
    /// <summary>Verifies nullable concrete value types retain the rule's direct-conversion behavior.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullableRoundTripIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync("class C { int? M(int? value) => {|PSH1015:(int?)(object)value|}; }");

    /// <summary>Verifies unresolved source or target types do not produce speculative conversion diagnostics.</summary>
    /// <param name="source">The source containing an unresolved type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C { Missing M(int value) => (Missing)(object)value; }")]
    [Arguments("class C { int M(Missing value) => (int)(object)value; }")]
    public Task UnresolvedTypesAreCleanAsync(string source) =>
        new Verify.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Verifies object spellings and extra grouping still identify a boxed round trip.</summary>
    /// <param name="expression">The round-trip expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("(int)((object)value)")]
    [Arguments("(int)(Object)value")]
    [Arguments("(int)(System.Object)value")]
    [Arguments("(int)(global::System.Object)value")]
    public Task ObjectSpellingsAreReportedAsync(string expression) =>
        Verify.VerifyAnalyzerAsync($$"""using System; class C { int M(int value) => {|PSH1015:{{expression}}|}; }""");

    /// <summary>Verifies casts without a built-in concrete value conversion are ignored.</summary>
    /// <param name="source">The nonmatching casts and their type declarations.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C { int M(int value) => (int)(long)value; }")]
    [Arguments("class C { int M(int value) => (int)(System.Int64)value; }")]
    [Arguments("using Number = System.Int64; class C { int M(int value) => (int)(Number)value; }")]
    [Arguments("class C { int? M(int value) => (int?)(int?)value; }")]
    [Arguments("class C { int M() => (int)(object)null; }")]
    [Arguments("class C { object M(int value) => (object)(object)value; }")]
    [Arguments("class C { T M<T>(int value) where T : struct => (T)(object)value; }")]
    [Arguments("struct A { } struct B { } class C { B M(A value) => (B)(object)value; }")]
    [Arguments("struct A { public static explicit operator int(A value) => 0; } class C { int M(A value) => (int)(object)value; }")]
    public Task NonmatchingConversionsAreCleanAsync(string source) => Verify.VerifyAnalyzerAsync(source);

    /// <summary>Verifies an enum-to-int round trip through object is flagged and cast directly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumRoundTripIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(DayOfWeek day) => {|PSH1015:(int)(object)day|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(DayOfWeek day) => (int)day;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an identity round trip is flagged and collapses to a direct cast.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityRoundTripIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int value) => {|PSH1015:(int)(object)value|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int value) => (int)value;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the generic specialization pattern stays clean; the JIT elides that box.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TypeParameterRoundTripIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public static int M<T>(T value) where T : struct
                    => typeof(T) == typeof(int) ? (int)(object)value : 0;
            }
            """);

    /// <summary>Verifies a cast from a reference type through object stays clean; nothing boxes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReferenceTypeCastIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M(object value) => (string)(object)value;
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
