// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyComparisonPattern = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2248UseComparisonPatternAnalyzer,
    StyleSharp.Analyzers.Sst2248UseComparisonPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2248UseComparisonPatternAnalyzer"/> and its code fix (SST2248).</summary>
public class UseComparisonPatternAnalyzerUnitTest
{
    /// <summary>Verifies a bounded range folds into an <c>and</c> pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RangeIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int x", "{|SST2248:x >= 0 && x <= 9|}"),
            Wrap("int x", "x is >= 0 and <= 9"));

    /// <summary>Verifies a range written upper bound first still folds, keeping operand order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UpperThenLowerRangeIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int x", "{|SST2248:x <= 9 && x >= 0|}"),
            Wrap("int x", "x is <= 9 and >= 0"));

    /// <summary>Verifies strict bounds with the constant on the left flip to a subject-on-left pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StrictRangeConstantOnLeftIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int x", "{|SST2248:0 < x && x < 9|}"),
            Wrap("int x", "x is > 0 and < 9"));

    /// <summary>Verifies an equality set over an enum folds into an <c>or</c> pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualitySetIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            WrapEnum("Color t", "{|SST2248:t == Color.Red || t == Color.Blue|}"),
            WrapEnum("Color t", "t is Color.Red or Color.Blue"));

    /// <summary>Verifies the region outside a range folds into an <c>or</c> pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutsideRangeIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int n", "{|SST2248:n < 0 || n > 100|}"),
            Wrap("int n", "n is < 0 or > 100"));

    /// <summary>Verifies the outside-range shape folds when the high side is written first.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutsideRangeReversedIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int n", "{|SST2248:n > 100 || n < 0|}"),
            Wrap("int n", "n is > 100 or < 0"));

    /// <summary>Verifies a pair of inequalities folds into a negated <c>and</c> pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InequalityPairIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int x", "{|SST2248:x != 3 && x != 5|}"),
            Wrap("int x", "x is not 3 and not 5"));

    /// <summary>Verifies a range with the lower constant on the left flips its operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InclusiveLowerConstantOnLeftIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int x", "{|SST2248:0 <= x && x <= 9|}"),
            Wrap("int x", "x is >= 0 and <= 9"));

    /// <summary>Verifies a range with the upper constant on the left flips its operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InclusiveUpperConstantOnLeftIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int x", "{|SST2248:9 >= x && x >= 0|}"),
            Wrap("int x", "x is <= 9 and >= 0"));

    /// <summary>Verifies a strict upper constant on the left flips its operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StrictUpperConstantOnLeftIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int x", "{|SST2248:100 > x && x > 5|}"),
            Wrap("int x", "x is < 100 and > 5"));

    /// <summary>Verifies an equality with the constant on the left keeps its operator when folding.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityConstantOnLeftIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("int x", "{|SST2248:5 == x || x == 7|}"),
            Wrap("int x", "x is 5 or 7"));

    /// <summary>Verifies a char range folds, carrying the char literals through.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CharRangeIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("char c", "{|SST2248:c >= 'a' && c <= 'z'|}"),
            Wrap("char c", "c is >= 'a' and <= 'z'"));

    /// <summary>Verifies a byte range folds even though the literal bounds are typed as int.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByteRangeIsFlaggedAndFixedAsync()
        => await VerifyComparisonPattern.VerifyCodeFixAsync(
            Wrap("byte b", "{|SST2248:b >= 2 && b <= 9|}"),
            Wrap("byte b", "b is >= 2 and <= 9"));

    /// <summary>Verifies a local read is treated as a foldable subject.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalSubjectIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int value)
                                  {
                                      var y = value;
                                      return {|SST2248:y >= 0 && y <= 9|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int value)
                                       {
                                           var y = value;
                                           return y is >= 0 and <= 9;
                                       }
                                   }
                                   """;
        await VerifyComparisonPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a field read is treated as a foldable subject.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldSubjectIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private readonly int _field = 5;

                                  public bool M() => {|SST2248:_field >= 0 && _field <= 9|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private readonly int _field = 5;

                                       public bool M() => _field is >= 0 and <= 9;
                                   }
                                   """;
        await VerifyComparisonPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies constant fields are usable as pattern bounds, folding across the flip.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantFieldBoundsAreFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private const int Low = 0;
                                  private const int High = 10;

                                  public bool M(int x) => {|SST2248:Low <= x && x < High|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private const int Low = 0;
                                       private const int High = 10;

                                       public bool M(int x) => x is >= Low and < High;
                                   }
                                   """;
        await VerifyComparisonPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies comparisons of two different subjects are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentSubjectsAreCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x, int y", "x >= 0 && y <= 9"));

    /// <summary>Verifies a side-effecting method-call subject is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodCallSubjectIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int Value() => 3;

                public bool M() => Value() >= 0 && Value() <= 9;
            }
            """);

    /// <summary>Verifies a property subject is left alone; folding two reads into one could change behaviour.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertySubjectIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int P { get; set; }

                public bool M() => P >= 0 && P <= 9;
            }
            """);

    /// <summary>Verifies a bound that is not a constant is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantBoundIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x, int lo, int hi", "x >= lo && x <= hi"));

    /// <summary>Verifies a string equality set is left alone; relational patterns are not valid there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringSubjectIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("string s", "s == \"a\" || s == \"b\""));

    /// <summary>Verifies a nullable subject is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableSubjectIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int? n", "n >= 0 && n <= 9"));

    /// <summary>Verifies a long bound against an int subject is left alone; the pattern would not compile.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LongBoundOnIntSubjectIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x", "x >= 0L && x <= 9L"));

    /// <summary>Verifies an empty range is left alone so the fix cannot produce a never-matching pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyRangeIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x", "x >= 9 && x <= 0"));

    /// <summary>Verifies an always-true disjunction is left alone so the fix cannot produce a warning.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TautologicalDisjunctionIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x", "x >= 0 || x <= 9"));

    /// <summary>Verifies a contradictory equality conjunction is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContradictoryConjunctionIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x", "x == 1 && x == 2"));

    /// <summary>Verifies two bounds in the same direction are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameDirectionBoundsAreCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x", "x >= 0 && x >= 5"));

    /// <summary>Verifies an always-true inequality disjunction is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InequalityDisjunctionIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x", "x != 3 || x != 5"));

    /// <summary>Verifies a mixed equality-and-relational disjunction is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedDisjunctionIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x", "x == 3 || x > 100"));

    /// <summary>Verifies a comparison joined with a non-comparison operand is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonComparisonOperandIsCleanAsync()
        => await VerifyComparisonPattern.VerifyAnalyzerAsync(Wrap("int x, bool flag", "x >= 0 && flag"));

    /// <summary>Verifies the rule stays silent below C# 9, where relational and logical patterns do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp9Async()
    {
        var test = new VerifyComparisonPattern.Test { TestCode = Wrap("int x", "x >= 0 && x <= 9") };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp8));
        });
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Wraps an expression body around a boolean expression over the given parameters.</summary>
    /// <param name="parameters">The method parameter list.</param>
    /// <param name="expression">The boolean expression, optionally carrying diagnostic markup.</param>
    /// <returns>The full source document.</returns>
    private static string Wrap(string parameters, string expression)
        => $$"""
             internal class C
             {
                 public bool M({{parameters}}) => {{expression}};
             }
             """;

    /// <summary>Wraps an expression body and a supporting enum around a boolean expression.</summary>
    /// <param name="parameters">The method parameter list.</param>
    /// <param name="expression">The boolean expression, optionally carrying diagnostic markup.</param>
    /// <returns>The full source document.</returns>
    private static string WrapEnum(string parameters, string expression)
        => $$"""
             internal enum Color
             {
                 Red,
                 Green,
                 Blue,
             }

             internal class C
             {
                 public bool M({{parameters}}) => {{expression}};
             }
             """;
}
