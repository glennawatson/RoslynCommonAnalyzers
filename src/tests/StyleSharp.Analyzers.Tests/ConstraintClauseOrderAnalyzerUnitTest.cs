// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConstraintOrder = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1221ConstraintClauseOrderAnalyzer,
    StyleSharp.Analyzers.Sst1221ConstraintClauseOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the constraint-clause order rule (SST1221) and its reorder fix.</summary>
public class ConstraintClauseOrderAnalyzerUnitTest
{
    /// <summary>Verifies out-of-order type constraint clauses are reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedTypeConstraintsReportedAsync()
    {
        const string Source = """
                              public class C<TKey, TValue>
                                  where TValue : class
                                  {|SST1221:where TKey : new()|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   public class C<TKey, TValue>
                                       where TKey : new()
                                       where TValue : class
                                   {
                                   }
                                   """;
        await VerifyConstraintOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies out-of-order method constraint clauses are reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedMethodConstraintsReportedAsync()
    {
        const string Source = """
                              public class Holder
                              {
                                  public static void M<TA, TB>()
                                      where TB : class
                                      {|SST1221:where TA : new()|}
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class Holder
                                   {
                                       public static void M<TA, TB>()
                                           where TA : new()
                                           where TB : class
                                       {
                                       }
                                   }
                                   """;
        await VerifyConstraintOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies constraint clauses already in type-parameter order are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InOrderConstraintsAreCleanAsync()
        => await VerifyConstraintOrder.VerifyAnalyzerAsync(
            """
            public class C<TKey, TValue>
                where TKey : new()
                where TValue : class
            {
            }
            """);

    /// <summary>Verifies a single constraint clause is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleConstraintIsCleanAsync()
        => await VerifyConstraintOrder.VerifyAnalyzerAsync(
            """
            public class C<TKey, TValue>
                where TValue : class
            {
            }
            """);
}
