// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.NonShortCircuitOperatorAnalyzer,
    StyleSharp.Analyzers.Sst2415NonShortCircuitGuardCodeFixProvider>;
using VerifyGuard = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.NonShortCircuitOperatorAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2415 (a non-short-circuiting boolean guard whose right operand does work).</summary>
public class NonShortCircuitGuardAnalyzerUnitTest
{
    /// <summary>A guard whose right operand runs a call the left was meant to gate.</summary>
    private const string WorkingRightSource = """
        public sealed class C
        {
            public bool M(bool ready) => ready {|SST2415:&|} Compute();

            private static bool Compute() => true;
        }
        """;

    /// <summary>The guard after short-circuiting.</summary>
    private const string WorkingRightFixed = """
        public sealed class C
        {
            public bool M(bool ready) => ready && Compute();

            private static bool Compute() => true;
        }
        """;

    /// <summary>Verifies an eager boolean operator whose right operand does work is reported as SST2415.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WorkingRightOperandIsReportedAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(WorkingRightSource);

    /// <summary>Verifies two plain reads are the tidy SST1468 case, not the guard case.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainReadsAreTheTidyCaseAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(bool a, bool b) => a {|SST1468:&|} b;
            }
            """);

    /// <summary>Verifies an integer bitwise operation is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntegerBitwiseIsCleanAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int flags, int mask) => flags & mask;
            }
            """);

    /// <summary>Verifies the fix short-circuits the guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixShortCircuitsTheGuardAsync()
        => await VerifyFix.VerifyCodeFixAsync(WorkingRightSource, WorkingRightFixed);
}
