// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2416SignedRemainderTestAnalyzer,
    StyleSharp.Analyzers.Sst2416SignedRemainderTestCodeFixProvider>;
using VerifyRemainder = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2416SignedRemainderTestAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2416 (a remainder parity test on a signed operand).</summary>
public class SignedRemainderTestAnalyzerUnitTest
{
    /// <summary>The source whose odd-parity remainder test on a signed operand is reported.</summary>
    private const string OddParityTestSource = """
                                               public sealed class C
                                               {
                                                   public bool M(int n) => {|SST2416:n % 2 == 1|};
                                               }
                                               """;

    /// <summary>Verifies the odd test on a signed int is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OddTestOnSignedIntIsReportedAsync() => VerifyRemainder.VerifyAnalyzerAsync(OddParityTestSource);

    /// <summary>Verifies the not-equal parity test is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NotEqualParityTestIsReportedAsync() =>
        VerifyRemainder.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int n) => {|SST2416:n % 2 != 1|};
            }
            """);

    /// <summary>Verifies the correct zero comparison is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ZeroComparisonIsCleanAsync() =>
        VerifyRemainder.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int n) => n % 2 == 0;
            }
            """);

    /// <summary>Verifies a count operand, which cannot be negative, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CountOperandIsCleanAsync() =>
        VerifyRemainder.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public bool M(List<int> items) => items.Count % 2 == 1;
            }
            """);

    /// <summary>Verifies an unsigned operand is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsignedOperandIsCleanAsync() =>
        VerifyRemainder.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(uint n) => n % 2 == 1;
            }
            """);

    /// <summary>Verifies an absolute value, which cannot be negative, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AbsoluteValueIsCleanAsync() =>
        VerifyRemainder.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public bool M(int n) => Math.Abs(n) % 2 == 1;
            }
            """);

    /// <summary>Verifies reversed comparisons and every supported signed numeric type are reported.</summary>
    /// <param name="type">The dividend type.</param>
    /// <param name="divisor">The nonzero integral divisor.</param>
    /// <param name="remainder">The nonzero integral comparison value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("sbyte", "(sbyte)2", "(byte)1")]
    [Arguments("short", "(short)2", "(ushort)1")]
    [Arguments("int", "2", "1")]
    [Arguments("long", "2L", "1U")]
    [Arguments("nint", "2", "1")]
    [Arguments("decimal", "2UL", "1")]
    public Task ReversedSignedComparisonsAreReportedAsync(string type, string divisor, string remainder) =>
        new VerifyRemainder.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = $$"""class C { bool M({{type}} n) => {|SST2416:{{remainder}} == n % {{divisor}}|}; }""" }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies comparisons outside the signed, integral, nonzero-constant shape are ignored.</summary>
    /// <param name="expression">The near-miss comparison.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("n == 1")]
    [Arguments("n != 1")]
    [Arguments("n % divisor == 1")]
    [Arguments("n % 0 == 1")]
    [Arguments("n % 2 == divisor")]
    [Arguments("n % 2 == null")]
    [Arguments("n % 2.0 == 1")]
    [Arguments("n % 2 == 1.0")]
    [Arguments("((double)n) % 2 == 1")]
    [Arguments("((int?)n) % 2 == 1")]
    [Arguments("((ulong)n) % 2 == 1")]
    [Arguments("text.Length % 2 == 1")]
    [Arguments("(n % 2) == 1")]
    [Arguments("null % 2 == 1")]
    public Task NonmatchingComparisonsAreCleanAsync(string expression) =>
        new VerifyRemainder.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net80,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $$"""class C { bool M(int n, int divisor, string text) => {{expression}}; }""",
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies ordinary members and non-absolute-value calls can still yield negative dividends.</summary>
    /// <param name="expression">The signed dividend.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("this.Value")]
    [Arguments("System.Math.Sign(n)")]
    [Arguments("Read(n)")]
    public Task PotentiallyNegativeMemberAndCallResultsAreReportedAsync(string expression) =>
        VerifyRemainder.VerifyAnalyzerAsync($$"""
            class C
            {
                int Value => -1;
                static int Read(int n) => n;
                bool M(int n) => {|SST2416:{{expression}} % 2 == 1|};
            }
            """);

    /// <summary>Verifies only the numerics namespace gives a BigInteger-shaped operand signed semantics.</summary>
    /// <param name="typeNamespace">The namespace defining the operator stub.</param>
    /// <param name="comparison">The expected analyzer markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("System.Numerics", "{|SST2416:n % 2 == 1|}")]
    [Arguments("Other", "n % 2 == 1")]
    public Task BigIntegerMustBelongToNumericsAsync(string typeNamespace, string comparison) =>
        VerifyRemainder.VerifyAnalyzerAsync($$"""
            namespace {{typeNamespace}}
            {
                public struct BigInteger
                {
                    public static int operator %(BigInteger value, int divisor) => 0;
                }
            }
            class C { bool M({{typeNamespace}}.BigInteger n) => {{comparison}}; }
            """);

    /// <summary>Verifies the fix promotes the generic-math helper where it exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixPromotesIsOddIntegerWhereAvailableAsync()
    {
        var test = new VerifyFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = OddParityTestSource,
            FixedCode = """
                public sealed class C
                {
                    public bool M(int n) => int.IsOddInteger(n);
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the not-equal parity test becomes the even-integer helper.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixPromotesIsEvenIntegerWhereAvailableAsync()
    {
        var test = new VerifyFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                public sealed class C
                {
                    public bool M(int n) => {|SST2416:n % 2 != 1|};
                }
                """,
            FixedCode = """
                public sealed class C
                {
                    public bool M(int n) => int.IsEvenInteger(n);
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the fix falls back to a zero comparison where the helper is absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixFallsBackWhereHelperIsAbsentAsync()
    {
        var test = new VerifyFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = OddParityTestSource,
            FixedCode = """
                public sealed class C
                {
                    public bool M(int n) => n % 2 != 0;
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
