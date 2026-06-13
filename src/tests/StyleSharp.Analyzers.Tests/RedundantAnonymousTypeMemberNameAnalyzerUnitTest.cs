// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedundantName = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.RedundantAnonymousTypeMemberNameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1173 (redundant anonymous-type member names) and its fix.</summary>
public class RedundantAnonymousTypeMemberNameAnalyzerUnitTest
{
    /// <summary>Verifies a member name matching a member-access expression's name is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantMemberAccessNameRemovedAsync()
    {
        const string Source = """
                              public class P
                              {
                                  public int X { get; set; }

                                  public int Y { get; set; }
                              }

                              public class C
                              {
                                  public object M(P p) => new { {|SST1173:X|} = p.X, Renamed = p.Y };
                              }
                              """;
        const string FixedSource = """
                                   public class P
                                   {
                                       public int X { get; set; }

                                       public int Y { get; set; }
                                   }

                                   public class C
                                   {
                                       public object M(P p) => new { p.X, Renamed = p.Y };
                                   }
                                   """;
        await VerifyRedundantName.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member name matching a local identifier is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantIdentifierNameRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public object M(int count) => new { {|SST1173:count|} = count };
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public object M(int count) => new { count };
                                   }
                                   """;
        await VerifyRedundantName.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member name that differs from the inferred name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DistinctNameIsCleanAsync()
        => await VerifyRedundantName.VerifyAnalyzerAsync(
            """
            public class P
            {
                public int X { get; set; }
            }

            public class C
            {
                public object M(P p) => new { Renamed = p.X, Sum = p.X + 1 };
            }
            """);
}
