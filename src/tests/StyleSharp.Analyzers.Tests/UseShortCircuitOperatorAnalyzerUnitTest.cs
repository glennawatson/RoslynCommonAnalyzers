// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyShortCircuit = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1468UseShortCircuitOperatorAnalyzer,
    StyleSharp.Analyzers.Sst1468UseShortCircuitOperatorCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1468 (boolean logic should short-circuit) and its fix.</summary>
public class UseShortCircuitOperatorAnalyzerUnitTest
{
    /// <summary>Verifies a boolean <c>&amp;</c> is reported and rewritten as <c>&amp;&amp;</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BooleanAndIsRewrittenAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(bool left, bool right) => left {|SST1468:&|} right;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(bool left, bool right) => left && right;
                                   }
                                   """;
        await VerifyShortCircuit.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a boolean <c>|</c> is reported and rewritten as <c>||</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BooleanOrIsRewrittenAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(bool left, bool right) => left {|SST1468:||} right;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(bool left, bool right) => left || right;
                                   }
                                   """;
        await VerifyShortCircuit.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comparison right operand is whitelisted and the precedence-safe rewrite keeps the comparison intact.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonRightOperandIsRewrittenAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(bool flag, int value) => flag {|SST1468:&|} value == 3;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(bool flag, int value) => flag && value == 3;
                                   }
                                   """;
        await VerifyShortCircuit.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a negated member-access chain right operand is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessChainRightOperandIsRewrittenAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public C Inner => this;

                                  public bool Flag => true;

                                  public bool M(bool ready, C other) => ready {|SST1468:&|} !other.Inner.Flag;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public C Inner => this;

                                       public bool Flag => true;

                                       public bool M(bool ready, C other) => ready && !other.Inner.Flag;
                                   }
                                   """;
        await VerifyShortCircuit.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies enum-flag and integer masking stays clean because the operands are not boolean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumAndIntegerOperandsAreCleanAsync()
        => await VerifyShortCircuit.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                [Flags]
                public enum Modes
                {
                    None = 0,
                    First = 1,
                    Second = 2,
                }

                public Modes Mask(Modes flags) => flags & Modes.First;

                public int Bits(int flags, int mask) => flags & mask;
            }
            """);

    /// <summary>Verifies an invocation right operand is not reported because skipping it could change behavior.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvocationRightOperandIsCleanAsync()
        => await VerifyShortCircuit.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(bool flag) => flag & Next();

                private static bool Next() => true;
            }
            """);

    /// <summary>Verifies a lambda converted to an expression tree is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionTreeLambdaIsCleanAsync()
        => await VerifyShortCircuit.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq.Expressions;

            public sealed class C
            {
                public Expression<Func<bool, bool, bool>> M() => (left, right) => left & right;
            }
            """);

    /// <summary>Verifies a lambda converted to a plain delegate is still reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateLambdaIsRewrittenAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public Func<bool, bool, bool> M() => (left, right) => left {|SST1468:&|} right;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public Func<bool, bool, bool> M() => (left, right) => left && right;
                                   }
                                   """;
        await VerifyShortCircuit.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the compound assignment forms are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundAssignmentsAreCleanAsync()
        => await VerifyShortCircuit.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(bool left, bool right)
                {
                    left &= right;
                    left |= right;
                    return left;
                }
            }
            """);

    /// <summary>Verifies the fix parenthesizes the rewrite when a tighter-binding parent operator would otherwise regroup it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedOperandKeepsGroupingWithParenthesesAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(bool flag, bool other) => flag {|SST1468:&|} other | Next();

                                  private static bool Next() => true;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(bool flag, bool other) => (flag && other) | Next();

                                       private static bool Next() => true;
                                   }
                                   """;
        await VerifyShortCircuit.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the document-based Fix All rewrites every occurrence in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool Both(bool left, bool right) => left {|SST1468:&|} right;

                                  public bool Either(bool left, bool right) => left {|SST1468:||} right;

                                  public bool Compare(bool left, int value) => left {|SST1468:&|} value == 3;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool Both(bool left, bool right) => left && right;

                                       public bool Either(bool left, bool right) => left || right;

                                       public bool Compare(bool left, int value) => left && value == 3;
                                   }
                                   """;
        await VerifyShortCircuit.VerifyCodeFixAsync(Source, FixedSource);
    }
}
