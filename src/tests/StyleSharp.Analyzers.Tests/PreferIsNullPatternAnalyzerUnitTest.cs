// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNull = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1149PreferIsNullPatternAnalyzer,
    StyleSharp.Analyzers.Sst1149PreferIsNullPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1149 (prefer <c>is null</c> / <c>is not null</c>) and its fix.</summary>
public class PreferIsNullPatternAnalyzerUnitTest
{
    /// <summary>Verifies null comparisons are reported and rewritten to the pattern form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullComparisonsRewrittenAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M1(object x) => {|SST1149:x == null|};

                                  public bool M2(object x) => {|SST1149:null == x|};

                                  public bool M3(object x) => {|SST1149:x != null|};

                                  public bool M4(bool choose, object left, object right) => {|SST1149:(choose ? left : right) == null|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M1(object x) => x is null;

                                       public bool M2(object x) => x is null;

                                       public bool M3(object x) => x is not null;

                                       public bool M4(bool choose, object left, object right) => (choose ? left : right) is null;
                                   }
                                   """;
        await VerifyNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies null comparisons inside expression-tree lambdas are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionTreeNullComparisonIsCleanAsync()
        => await VerifyNull.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public Expression<Func<object, bool>> M() => x => x == null;
            }
            """);

    /// <summary>Verifies the document-based Fix All rewrites every occurrence in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(object a, object b, object c, object d)
                                  {
                                      var w = {|SST1149:a == null|};
                                      var x = {|SST1149:b != null|};
                                      var y = {|SST1149:null == c|};
                                      var z = {|SST1149:d == null|};
                                      return w && x && y && z;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(object a, object b, object c, object d)
                                       {
                                           var w = a is null;
                                           var x = b is not null;
                                           var y = c is null;
                                           var z = d is null;
                                           return w && x && y && z;
                                       }
                                   }
                                   """;

        await VerifyNull.VerifyCodeFixAsync(Source, FixedSource);
    }
}
