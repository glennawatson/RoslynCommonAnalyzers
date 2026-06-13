// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDoubledNegation = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.DoubledNegationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1190 (doubled negation) and its fix.</summary>
public class DoubledNegationAnalyzerUnitTest
{
    /// <summary>Verifies <c>!!x</c> is reported and collapsed to <c>x</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DoubleNotCollapsedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(bool flag) => {|SST1190:!!flag|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(bool flag) => flag;
                                   }
                                   """;
        await VerifyDoubledNegation.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a triple negation is reported once and collapses to a single operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TripleNotCollapsesToSingleAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(bool flag) => {|SST1190:!!!flag|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(bool flag) => !flag;
                                   }
                                   """;
        await VerifyDoubledNegation.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a single negation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleNegationIsCleanAsync()
        => await VerifyDoubledNegation.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool M(bool flag) => !flag;
            }
            """);
}
