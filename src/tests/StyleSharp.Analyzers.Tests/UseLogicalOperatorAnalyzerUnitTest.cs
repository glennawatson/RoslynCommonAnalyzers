// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUseLogicalOperator = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2288UseLogicalOperatorAnalyzer,
    StyleSharp.Analyzers.Sst2288UseLogicalOperatorCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2288UseLogicalOperatorAnalyzer"/> and its code fix (SST2288).</summary>
public class UseLogicalOperatorAnalyzerUnitTest
{
    /// <summary>Verifies a false else-branch folds to a conjunction.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FalseElseBranchBecomesConjunctionAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool a, bool b) => {|SST2288:a ? b : false|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool a, bool b) => a && b;
                                   }
                                   """;
        await VerifyUseLogicalOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a true then-branch folds to a disjunction.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrueThenBranchBecomesDisjunctionAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool a, bool b) => {|SST2288:a ? true : b|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool a, bool b) => a || b;
                                   }
                                   """;
        await VerifyUseLogicalOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a false then-branch negates the condition and conjoins.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FalseThenBranchNegatesAndConjoinsAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool a, bool b) => {|SST2288:a ? false : b|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool a, bool b) => !a && b;
                                   }
                                   """;
        await VerifyUseLogicalOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a true else-branch negates the condition and disjoins.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrueElseBranchNegatesAndDisjoinsAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool a, bool b) => {|SST2288:a ? b : true|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool a, bool b) => !a || b;
                                   }
                                   """;
        await VerifyUseLogicalOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an already-negated condition is unwrapped rather than doubled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedConditionIsUnwrappedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool a, bool b) => {|SST2288:!a ? false : b|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool a, bool b) => a && b;
                                   }
                                   """;
        await VerifyUseLogicalOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comparison condition is parenthesized when negated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedComparisonIsParenthesizedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int count, bool b) => {|SST2288:count > 0 ? false : b|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int count, bool b) => !(count > 0) && b;
                                   }
                                   """;
        await VerifyUseLogicalOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a disjunction branch is parenthesized inside a conjunction.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisjunctionBranchIsParenthesizedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool a, bool b, bool c) => {|SST2288:a ? b || c : false|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool a, bool b, bool c) => a && (b || c);
                                   }
                                   """;
        await VerifyUseLogicalOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a conditional with two literal branches is left to the rule that owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BothBranchesLiteralIsCleanAsync()
        => await VerifyUseLogicalOperator.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(bool a) => a ? true : false;
            }
            """);

    /// <summary>Verifies a conditional with no literal branch is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoLiteralBranchIsCleanAsync()
        => await VerifyUseLogicalOperator.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(bool a, bool b, bool c) => a ? b : c;
            }
            """);

    /// <summary>Verifies a non-boolean conditional is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonBooleanConditionalIsCleanAsync()
        => await VerifyUseLogicalOperator.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(bool a) => a ? 1 : 2;
            }
            """);
}
