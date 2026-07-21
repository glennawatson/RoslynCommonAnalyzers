// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2495RedundantFlagsOperandAnalyzer,
    StyleSharp.Analyzers.Sst2495RedundantFlagsOperandCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2495 (a flags operand whose bits another operand already sets).</summary>
public class Sst2495RedundantFlagsOperandAnalyzerUnitTest
{
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
    [Test]
    public async Task DisjointAndNonFlagsAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
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
    [Test]
    public async Task NonConstantOperandIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            [System.Flags]
            public enum F { A = 1, B = 2 }

            public sealed class C
            {
                public F M(F other) => F.A | other;
            }
            """);
}
