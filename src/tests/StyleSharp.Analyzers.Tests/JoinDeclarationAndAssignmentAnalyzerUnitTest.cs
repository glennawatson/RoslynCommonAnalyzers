// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyJoin = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2250JoinDeclarationAndAssignmentAnalyzer,
    StyleSharp.Analyzers.Sst2250JoinDeclarationAndAssignmentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2250JoinDeclarationAndAssignmentAnalyzer"/> and its code fix (SST2250).</summary>
public class JoinDeclarationAndAssignmentAnalyzerUnitTest
{
    /// <summary>Verifies a bare declaration and its next-statement assignment are joined.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdjacentDeclarationAndAssignmentAreJoinedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M()
                                  {
                                      int {|SST2250:x|};
                                      x = 5;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M()
                                       {
                                           int x = 5;
                                       }
                                   }
                                   """;
        await VerifyJoin.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the joined initializer keeps a non-literal value expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralValueIsJoinedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public string M(string a, string b)
                                  {
                                      string {|SST2250:s|};
                                      s = a + b;
                                      return s;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public string M(string a, string b)
                                       {
                                           string s = a + b;
                                           return s;
                                       }
                                   }
                                   """;
        await VerifyJoin.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies surrounding statements and their trivia survive the join.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SurroundingStatementsSurviveTheFixAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int M(int seed)
                                  {
                                      // Compute the running total.
                                      int {|SST2250:total|};
                                      total = seed * 2;
                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int M(int seed)
                                       {
                                           // Compute the running total.
                                           int total = seed * 2;
                                           return total;
                                       }
                                   }
                                   """;
        await VerifyJoin.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a later read of the same local does not stop the first-assignment join.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LaterReadDoesNotStopTheJoinAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int M()
                                  {
                                      int {|SST2250:x|};
                                      x = 5;
                                      return x + 1;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int M()
                                       {
                                           int x = 5;
                                           return x + 1;
                                       }
                                   }
                                   """;
        await VerifyJoin.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-declarator declaration is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleDeclaratorsAreCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    int x, y;
                    x = 5;
                    y = 6;
                }
            }
            """);

    /// <summary>Verifies an already-initialized declaration is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializedDeclarationIsCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M()
                {
                    int x = 0;
                    x = 5;
                    return x;
                }
            }
            """);

    /// <summary>Verifies a conditional first write keeps the declaration and assignment apart.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalFirstAssignmentIsCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(bool flag)
                {
                    int x;
                    if (flag)
                    {
                        x = 5;
                    }
                    else
                    {
                        x = 6;
                    }

                    return x;
                }
            }
            """);

    /// <summary>Verifies a use of the local before the assignment keeps them apart.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UseBetweenDeclarationAndAssignmentIsCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M()
                {
                    int x;
                    int.TryParse("5", out x);
                    x = 10;
                    return x;
                }
            }
            """);

    /// <summary>Verifies a declaration that is the last statement in its block is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclarationAsLastStatementIsCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    int x;
                }
            }
            """);

    /// <summary>Verifies a following assignment to a different local is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignmentToDifferentLocalIsCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M()
                {
                    int x;
                    int y = 0;
                    y = 5;
                    x = y;
                    return x;
                }
            }
            """);

    /// <summary>Verifies a following member assignment is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FollowingMemberAssignmentIsCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int _value;

                public void M()
                {
                    int x;
                    this._value = 5;
                    x = 1;
                    System.Console.WriteLine(x);
                }
            }
            """);

    /// <summary>Verifies a following non-assignment statement is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FollowingNonAssignmentStatementIsCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    int x;
                    System.Console.WriteLine();
                    x = 5;
                    _ = x;
                }
            }
            """);

    /// <summary>Verifies a declaration in a switch section, which is not a block, is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclarationInSwitchSectionIsCleanAsync()
        => await VerifyJoin.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int input)
                {
                    switch (input)
                    {
                        case 0:
                            int x;
                            x = 5;
                            return x;
                        default:
                            return -1;
                    }
                }
            }
            """);
}
