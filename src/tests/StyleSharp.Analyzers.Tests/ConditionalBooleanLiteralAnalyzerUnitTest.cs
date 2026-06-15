// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConditional = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.ConditionalBooleanLiteralCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1182 (conditional returning boolean literals) and its fix.</summary>
public class ConditionalBooleanLiteralAnalyzerUnitTest
{
    /// <summary>Verifies <c>c ? true : false</c> is reported and replaced by the condition.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrueFalseReplacedByConditionAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(int value) => {|SST1182:value > 0 ? true : false|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(int value) => value > 0;
                                   }
                                   """;
        await VerifyConditional.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>c ? false : true</c> is reported and replaced by the negated condition.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FalseTrueReplacedByNegationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(bool flag) => {|SST1182:flag ? false : true|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(bool flag) => !flag;
                                   }
                                   """;
        await VerifyConditional.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every boolean-literal conditional in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool A(int value) => {|SST1182:value > 0 ? true : false|};

                                  public bool B(bool flag) => {|SST1182:flag ? false : true|};

                                  public bool D(int value) => {|SST1182:value < 10 ? true : false|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool A(int value) => value > 0;

                                       public bool B(bool flag) => !flag;

                                       public bool D(int value) => value < 10;
                                   }
                                   """;
        await VerifyConditional.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a conditional with non-literal branches is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralBranchesAreCleanAsync()
        => await VerifyConditional.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool M(int value, bool other) => value > 0 ? other : false;
            }
            """);
}
