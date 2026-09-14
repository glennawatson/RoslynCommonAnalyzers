// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2495RedundantFlagsOperandAnalyzer,
    StyleSharp.Analyzers.Sst2495RedundantFlagsOperandCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2495 (a flags operand whose bits another operand already sets).</summary>
public class Sst2495RedundantFlagsOperandAnalyzerUnitTest
{
    /// <summary>Verifies redundancy for every legal enum backing type, including signed high bits.</summary>
    /// <param name="underlyingType">The enum backing type.</param>
    /// <param name="all">A constant containing the low flag bit.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("int", "-1")]
    [Arguments("uint", "4294967295U")]
    [Arguments("long", "-1L")]
    [Arguments("ulong", "18446744073709551615UL")]
    [Arguments("short", "-1")]
    [Arguments("ushort", "65535")]
    [Arguments("sbyte", "-1")]
    [Arguments("byte", "255")]
    public Task IntegralBackingTypesReportSubsetAsync(string underlyingType, string all) =>
        Verify.VerifyAnalyzerAsync($$"""
            [System.Flags]
            public enum F : {{underlyingType}} { A = 1, All = {{all}} }
            class C { F M() => ({|SST2495:F.A|}) | (F.All); }
            """);

    /// <summary>Verifies nested chains flatten once while zero and unknown values remain untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ParenthesizedChainPreservesZeroAndUnknownOperandsAsync() =>
        Verify.VerifyAnalyzerAsync("""
            [System.Flags]
            public enum F { None = 0, A = 1, B = 2, Both = 3 }
            class C
            {
                F M(F unknown) => ((F.None | unknown) | (({|SST2495:F.A|}) | F.Both));
                F N() => (F.A & F.B) | F.None;
                F P(F? value) => (value ?? F.None) | F.A;
            }
            """);

    /// <summary>Verifies only the real System.FlagsAttribute enables redundancy diagnostics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedAttributesAndNonFlagsEnumsAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            namespace Other
            {
                public sealed class FlagsAttribute : System.Attribute { }
            }
            namespace Nested.System
            {
                public sealed class FlagsAttribute : global::System.Attribute { }
            }
            [Other.Flags] public enum F { A = 1, Both = 3 }
            [Nested.System.Flags] public enum G { A = 1, Both = 3 }
            [System.Serializable] public enum H { A = 1, Both = 3 }
            public enum I { A = 1, Both = 3 }
            class C
            {
                F M() => F.A | F.Both;
                G N() => G.A | G.Both;
                H O() => H.A | H.Both;
                I P() => I.A | I.Both;
                bool Q(bool a, bool b) => a | b;
            }
            """);

    /// <summary>Verifies a single flag already inside a composite operand is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubsetOperandIsRemovedAsync()
    {
        const string Source = """
            [System.Flags]
            public enum F { A = 1, B = 2, Both = A | B }

            public sealed class C
            {
                public F M() => F.Both | {|SST2495:F.A|};
            }
            """;
        const string Fixed = """
            [System.Flags]
            public enum F { A = 1, B = 2, Both = A | B }

            public sealed class C
            {
                public F M() => F.Both;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a repeated flag is reported once and the duplicate removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateOperandIsRemovedAsync()
    {
        const string Source = """
            [System.Flags]
            public enum F { A = 1, B = 2 }

            public sealed class C
            {
                public F M() => F.A | {|SST2495:F.A|};
            }
            """;
        const string Fixed = """
            [System.Flags]
            public enum F { A = 1, B = 2 }

            public sealed class C
            {
                public F M() => F.A;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a redundant operand in the middle of a chain is removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MiddleOperandIsRemovedAsync()
    {
        const string Source = """
            [System.Flags]
            public enum F { A = 1, B = 2, C = 4, Both = A | B }

            public sealed class C
            {
                public F M() => F.Both | {|SST2495:F.A|} | F.C;
            }
            """;
        const string Fixed = """
            [System.Flags]
            public enum F { A = 1, B = 2, C = 4, Both = A | B }

            public sealed class C
            {
                public F M() => F.Both | F.C;
            }
            """;
        await Verify.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a disjoint combination and a non-flags enum are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DisjointAndNonFlagsAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            [System.Flags]
            public enum F { A = 1, B = 2 }

            public enum G { X = 1, Y = 3 }

            public sealed class C
            {
                public F Flags() => F.A | F.B;
                public int Plain() => (int)G.X | (int)G.Y;
            }
            """);

    /// <summary>Verifies a non-constant operand cannot be proven redundant and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantOperandIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            [System.Flags]
            public enum F { A = 1, B = 2 }

            public sealed class C
            {
                public F M(F other) => F.A | other;
            }
            """);
}
