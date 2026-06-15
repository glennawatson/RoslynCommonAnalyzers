// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCoalesce = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1418NullCoalescingPrecedenceAnalyzer,
    StyleSharp.Analyzers.PrecedenceCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1418 (declare precedence when mixing the null-coalescing operator).</summary>
public class NullCoalescingPrecedenceAnalyzerUnitTest
{
    /// <summary>Verifies a binary operand of '??' is reported and parenthesized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BinaryOperandParenthesizedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int? a, int b, int c) => a ?? {|SST1418:b + c|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int? a, int b, int c) => a ?? (b + c);
                                   }
                                   """;
        await VerifyCoalesce.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All parenthesizes every '??' binary operand in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int? a, int b, int c) => a ?? {|SST1418:b + c|};

                                  public int N(int? d, int e, int f) => d ?? {|SST1418:e * f|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int? a, int b, int c) => a ?? (b + c);

                                       public int N(int? d, int e, int f) => d ?? (e * f);
                                   }
                                   """;
        await VerifyCoalesce.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies parenthesized operands and chained '??' are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedAndChainedAreCleanAsync()
        => await VerifyCoalesce.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int? a, int? d, int b, int c) => a ?? (b + c);

                public int N(int? a, int? d, int e) => a ?? d ?? e;
            }
            """);
}
