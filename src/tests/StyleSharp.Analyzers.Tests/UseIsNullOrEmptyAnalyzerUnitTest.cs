// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyIsNullOrEmpty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2255UseIsNullOrEmptyAnalyzer,
    StyleSharp.Analyzers.Sst2255UseIsNullOrEmptyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2255UseIsNullOrEmptyAnalyzer"/> and its code fix (SST2255).</summary>
public class UseIsNullOrEmptyAnalyzerUnitTest
{
    /// <summary>Verifies the null-or-length disjunction is reported and folded to the helper call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullOrLengthIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string s) => {|SST2255:s == null || s.Length == 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string s) => string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyIsNullOrEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the null-or-empty-string disjunction is reported and folded to the helper call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullOrEmptyStringIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string s) => {|SST2255:s == null || s == ""|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string s) => string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyIsNullOrEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the negated conjunction folds to the negated helper call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotNullAndNotEmptyIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string s) => {|SST2255:s != null && s.Length != 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string s) => !string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyIsNullOrEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the empty-string check may precede the null check and still fold.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyStringBeforeNullIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string s) => {|SST2255:s == "" || s == null|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string s) => string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyIsNullOrEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the null literal on the left of the comparison still folds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullLiteralOnLeftIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string s) => {|SST2255:null == s || s.Length == 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string s) => string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyIsNullOrEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the negated conjunction with an empty-string check folds to the negated helper call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotNullAndNotEmptyStringIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string s) => {|SST2255:s != null && s != ""|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string s) => !string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyIsNullOrEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies parenthesized operands are unwrapped and still fold.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedOperandsAreFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string s) => {|SST2255:(s == null) || (s.Length == 0)|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string s) => string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyIsNullOrEmpty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comparison of the wrong kind is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrongKindComparisonIsCleanAsync()
        => await VerifyIsNullOrEmpty.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(string s) => s == null || s.Length < 0;
            }
            """);

    /// <summary>Verifies a zero comparison against a member that is not Length is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLengthMemberIsCleanAsync()
        => await VerifyIsNullOrEmpty.VerifyAnalyzerAsync(
            """
            internal sealed class Box
            {
                public int Count { get; set; }
            }

            internal class C
            {
                public bool M(Box b) => b == null || b.Count == 0;
            }
            """);

    /// <summary>Verifies an operand that is not a comparison is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonComparisonOperandIsCleanAsync()
        => await VerifyIsNullOrEmpty.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(bool flag, string s) => flag || s == null;
            }
            """);

    /// <summary>Verifies an unrelated disjunction of two comparisons is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedComparisonsAreCleanAsync()
        => await VerifyIsNullOrEmpty.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(int a, int b) => a == 1 || b == 2;
            }
            """);

    /// <summary>Verifies two different values in the null and empty checks are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentValuesAreCleanAsync()
        => await VerifyIsNullOrEmpty.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(string s, string t) => s == null || t.Length == 0;
            }
            """);

    /// <summary>Verifies a length check before the null check is left alone; the order can throw.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthBeforeNullIsCleanAsync()
        => await VerifyIsNullOrEmpty.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(string s) => s.Length == 0 || s == null;
            }
            """);

    /// <summary>Verifies a non-string value with a Length member is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringLengthIsCleanAsync()
        => await VerifyIsNullOrEmpty.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(int[] a) => a == null || a.Length == 0;
            }
            """);

    /// <summary>Verifies a value with a side effect is left alone; the fold would change how often it runs.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SideEffectingValueIsCleanAsync()
        => await VerifyIsNullOrEmpty.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string Get() => "";

                public bool M() => Get() == null || Get().Length == 0;
            }
            """);
}
