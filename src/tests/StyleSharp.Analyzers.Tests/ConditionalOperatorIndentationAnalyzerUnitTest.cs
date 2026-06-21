// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConditionalOperatorIndentation = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1145ConditionalOperatorPlacementAnalyzer,
    StyleSharp.Analyzers.Sst1140ConditionalOperatorIndentationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1140 (start wrapped conditional operators on indented continuation lines).</summary>
public class ConditionalOperatorIndentationAnalyzerUnitTest
{
    /// <summary>Verifies trailing conditional operators are moved to indented branch-leading lines.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TrailingOperatorsAreReflowedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(bool c) => c {|SST1140:?|}
                                      1 {|SST1140::|}
                                      2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(bool c) => c
                                           ? 1
                                           : 2;
                                   }
                                   """;

        await VerifyConditionalOperatorIndentation.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies leading conditional operators at the wrong indentation are reindented.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LeadingOperatorsAtWrongIndentAreReflowedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(bool c) => c
                              {|SST1140:?|} 1
                              {|SST1140::|} 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(bool c) => c
                                           ? 1
                                           : 2;
                                   }
                                   """;

        await VerifyConditionalOperatorIndentation.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies correctly indented conditional operators are clean when branch expressions wrap later.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WrappedBranchesAfterLeadingOperatorsAreCleanAsync()
        => await VerifyConditionalOperatorIndentation.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(bool c) => c
                    ? Build(
                        1,
                        2)
                    : Build(
                        3,
                        4);

                private int Build(int x, int y) => x + y;
            }
            """);

    /// <summary>Verifies single-line conditionals are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SingleLineConditionalIsCleanAsync()
        => await VerifyConditionalOperatorIndentation.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(bool c) => c ? 1 : 2;
            }
            """);
}
