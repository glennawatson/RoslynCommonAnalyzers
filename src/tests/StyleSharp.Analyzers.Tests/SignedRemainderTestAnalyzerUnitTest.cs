// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2416SignedRemainderTestAnalyzer,
    StyleSharp.Analyzers.Sst2416SignedRemainderTestCodeFixProvider>;
using VerifyRemainder = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2416SignedRemainderTestAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2416 (a remainder parity test on a signed operand).</summary>
public class SignedRemainderTestAnalyzerUnitTest
{
    /// <summary>Verifies the odd test on a signed int is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OddTestOnSignedIntIsReportedAsync()
        => await VerifyRemainder.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int n) => {|SST2416:n % 2 == 1|};
            }
            """);

    /// <summary>Verifies the not-equal parity test is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotEqualParityTestIsReportedAsync()
        => await VerifyRemainder.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int n) => {|SST2416:n % 2 != 1|};
            }
            """);

    /// <summary>Verifies the correct zero comparison is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroComparisonIsCleanAsync()
        => await VerifyRemainder.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int n) => n % 2 == 0;
            }
            """);

    /// <summary>Verifies a count operand, which cannot be negative, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountOperandIsCleanAsync()
        => await VerifyRemainder.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public bool M(List<int> items) => items.Count % 2 == 1;
            }
            """);

    /// <summary>Verifies an unsigned operand is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedOperandIsCleanAsync()
        => await VerifyRemainder.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(uint n) => n % 2 == 1;
            }
            """);

    /// <summary>Verifies an absolute value, which cannot be negative, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbsoluteValueIsCleanAsync()
        => await VerifyRemainder.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public bool M(int n) => Math.Abs(n) % 2 == 1;
            }
            """);

    /// <summary>Verifies the fix promotes the generic-math helper where it exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixPromotesIsOddIntegerWhereAvailableAsync()
    {
        var test = new VerifyFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                public sealed class C
                {
                    public bool M(int n) => {|SST2416:n % 2 == 1|};
                }
                """,
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
            TestCode = """
                public sealed class C
                {
                    public bool M(int n) => {|SST2416:n % 2 == 1|};
                }
                """,
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
