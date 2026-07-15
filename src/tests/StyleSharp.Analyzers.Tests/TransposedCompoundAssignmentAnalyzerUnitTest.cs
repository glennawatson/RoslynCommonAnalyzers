// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2417TransposedCompoundAssignmentAnalyzer,
    StyleSharp.Analyzers.Sst2417TransposedCompoundAssignmentCodeFixProvider>;
using VerifySpacing = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.SpacingAnalyzer>;
using VerifyTransposed = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2417TransposedCompoundAssignmentAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2417 (an assignment spaced like a transposed operator).</summary>
public class TransposedCompoundAssignmentAnalyzerUnitTest
{
    /// <summary>The compound-operator fix's equivalence key.</summary>
    private const string CompoundKey = "Sst2417TransposedCompoundAssignmentCodeFixProvider.Compound";

    /// <summary>The unary-value fix's equivalence key.</summary>
    private const string UnaryKey = "Sst2417TransposedCompoundAssignmentCodeFixProvider.Unary";

    /// <summary>Verifies a transposed <c>+</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TransposedPlusIsReportedAsync()
        => await VerifyTransposed.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int x)
                {
                    x {|SST2417:=+|} 1;
                }
            }
            """);

    /// <summary>Verifies a transposed <c>-</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TransposedMinusIsReportedAsync()
        => await VerifyTransposed.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int x)
                {
                    x {|SST2417:=-|} 1;
                }
            }
            """);

    /// <summary>Verifies a transposed <c>!</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TransposedNotIsReportedAsync()
        => await VerifyTransposed.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(bool flag, bool other)
                {
                    flag {|SST2417:=!|} other;
                }
            }
            """);

    /// <summary>Verifies a deliberate unary assignment with a space after '=' is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeliberateUnaryIsCleanAsync()
        => await VerifyTransposed.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int x)
                {
                    x = +1;
                    x = -1;
                }
            }
            """);

    /// <summary>Verifies a real compound assignment is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundAssignmentIsCleanAsync()
        => await VerifyTransposed.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int x)
                {
                    x += 1;
                }
            }
            """);

    /// <summary>Verifies the closed-up form (no space after the sign) is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClosedUpFormIsCleanAsync()
        => await VerifyTransposed.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int x)
                {
                    x =+1;
                }
            }
            """);

    /// <summary>Verifies the spacing rules stay silent on the transposed span, preserving the evidence.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpacingRulesAreSuppressedOnTheTransposedSpanAsync()
        => await VerifySpacing.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int x)
                {
                    x =+ 1;
                }
            }
            """);

    /// <summary>Verifies the compound reading closes the gap into '+='.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundFixClosesTheGapAsync()
    {
        var test = new VerifyFix.Test
        {
            CodeActionEquivalenceKey = CompoundKey,
            TestCode = """
                public sealed class C
                {
                    public void M(int x)
                    {
                        x {|SST2417:=+|} 1;
                    }
                }
                """,
            FixedCode = """
                public sealed class C
                {
                    public void M(int x)
                    {
                        x += 1;
                    }
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the unary reading opens the gap into '= +'.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnaryFixOpensTheGapAsync()
    {
        var test = new VerifyFix.Test
        {
            CodeActionEquivalenceKey = UnaryKey,
            TestCode = """
                public sealed class C
                {
                    public void M(int x)
                    {
                        x {|SST2417:=+|} 1;
                    }
                }
                """,
            FixedCode = """
                public sealed class C
                {
                    public void M(int x)
                    {
                        x = +1;
                    }
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the negation case offers only the unary reading.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotFixAssignsTheNegationAsync()
    {
        var test = new VerifyFix.Test
        {
            CodeActionEquivalenceKey = UnaryKey,
            TestCode = """
                public sealed class C
                {
                    public void M(bool flag, bool other)
                    {
                        flag {|SST2417:=!|} other;
                    }
                }
                """,
            FixedCode = """
                public sealed class C
                {
                    public void M(bool flag, bool other)
                    {
                        flag = !other;
                    }
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
