// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLiteralOrder = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.LiteralOnRightOfComparisonCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1186 (literal on the left of a comparison) and its fix.</summary>
public class LiteralOnRightOfComparisonAnalyzerUnitTest
{
    /// <summary>Verifies <c>0 == count</c> is reported and swapped to <c>count == 0</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeftLiteralSwappedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(int count) => {|SST1186:0 == count|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(int count) => count == 0;
                                   }
                                   """;
        await VerifyLiteralOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an already-right literal, a null comparison, and a two-variable comparison are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConventionalComparisonsAreCleanAsync()
        => await VerifyLiteralOrder.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool RightLiteral(int count) => count != 0;

                public bool NullCheck(string text) => null == text;

                public bool TwoVariables(int a, int b) => a == b;
            }
            """);
}
