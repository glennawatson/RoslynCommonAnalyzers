// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyIdenticalBranches = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.IdenticalBranchesAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1476 (conditional branches should not have identical bodies).</summary>
public class IdenticalBranchesAnalyzerUnitTest
{
    /// <summary>Verifies an if/else whose two bodies are the same is reported on the if keyword.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IfElseWithIdenticalBodiesIsReportedAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Pick(bool flag)
                {
                    {|SST1476:if|} (flag)
                    {
                        return 1;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }
            """);

    /// <summary>Verifies a whole if/else-if/else chain of identical bodies is reported once.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IfChainWithIdenticalBodiesIsReportedAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Pick(int value)
                {
                    {|SST1476:if|} (value < 0)
                    {
                        return 2;
                    }
                    else if (value > 10)
                    {
                        return 2;
                    }
                    else
                    {
                        return 2;
                    }
                }
            }
            """);

    /// <summary>Verifies a braced body and a bare one are still the same duplicate.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BracesDoNotHideAnIdenticalBodyAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Pick(bool flag)
                {
                    {|SST1476:if|} (flag)
                        Run();
                    else
                    {
                        Run();
                    }
                }

                private static void Run()
                {
                }
            }
            """);

    /// <summary>Verifies an if with no else is not reported, because its unwritten branch does nothing.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IfWithoutElseIsCleanAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Pick(bool flag)
                {
                    if (flag)
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a chain that never reaches a trailing else is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ChainWithoutTerminalElseIsCleanAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Pick(int value)
                {
                    if (value < 0)
                    {
                        return 1;
                    }
                    else if (value > 10)
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies branches that actually differ are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DifferentBodiesAreCleanAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Pick(int value)
                {
                    if (value < 0)
                    {
                        return 1;
                    }
                    else if (value > 10)
                    {
                        return 2;
                    }
                    else
                    {
                        return 3;
                    }
                }
            }
            """);

    /// <summary>Verifies a conditional expression whose arms produce the same value is reported on the question mark.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConditionalExpressionWithIdenticalArmsIsReportedAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Pick(bool flag) => flag {|SST1476:?|} 4 : 4;

                public int Choose(bool flag) => flag ? 4 : 5;
            }
            """);

    /// <summary>Verifies a switch statement whose sections all run the same body is reported on the switch keyword.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SwitchStatementWithIdenticalSectionsIsReportedAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Route(int value)
                {
                    {|SST1476:switch|} (value)
                    {
                        case 1:
                            return 3;
                        case 2:
                            return 3;
                        default:
                            return 3;
                    }
                }
            }
            """);

    /// <summary>Verifies a switch statement with no default label is not the SST1476 all-branches case, but its two identical sections are the SST2414 pair.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>An unmatched value falls out of the switch, so SST1476 stays silent; the two identical sections are still a shared implementation.</remarks>
    [Test]
    public async Task SwitchStatementWithoutDefaultReportsDuplicatePairAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Route(int value)
                {
                    switch (value)
                    {
                        case 1:
                            return 3;
                        {|SST2414:case 2:|}
                            return 3;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a switch statement whose sections differ is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SwitchStatementWithDifferentSectionsIsCleanAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Route(int value)
                {
                    switch (value)
                    {
                        case 1:
                            return 1;
                        case 2:
                            return 2;
                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a switch expression whose arms produce the same value is reported on the switch keyword.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SwitchExpressionWithIdenticalArmsIsReportedAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name(int value) => value {|SST1476:switch|}
                {
                    1 => "same",
                    2 => "same",
                    _ => "same",
                };
            }
            """);

    /// <summary>Verifies a switch expression the compiler considers exhaustive is reported even without a discard arm.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Exhaustiveness is the compiler's own answer, so a complete bool switch counts without spelling out <c>_</c>.</remarks>
    [Test]
    public async Task ExhaustiveSwitchExpressionWithoutDiscardIsReportedAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name(bool flag) => flag {|SST1476:switch|}
                {
                    true => "same",
                    false => "same",
                };
            }
            """);

    /// <summary>Verifies a non-exhaustive switch expression is not the SST1476 all-arms case, but its two identical arms are the SST2414 pair.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonExhaustiveSwitchExpressionReportsDuplicatePairAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name(int value) => value switch
                {
                    1 => "same",
                    {|SST2414:2|} => "same",
                };
            }
            """);

    /// <summary>Verifies a switch expression whose arms differ is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SwitchExpressionWithDifferentArmsIsCleanAsync()
        => await VerifyIdenticalBranches.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name(int value) => value switch
                {
                    1 => "one",
                    2 => "two",
                    _ => "other",
                };
            }
            """);

    /// <summary>Verifies a single-statement body counts under the default minimum.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DefaultMinimumCountsASingleStatementAsync()
    {
        var test = new VerifyIdenticalBranches.Test
        {
            TestCode = """
                       public class C
                       {
                           public void Pick(bool flag)
                           {
                               {|SST1476:if|} (flag)
                               {
                                   Run();
                               }
                               else
                               {
                                   Run();
                               }
                           }

                           private static void Run()
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1476.minimum_statements = 1

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies raising the minimum suppresses the trivially short duplicates, including expression arms.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RaisedMinimumSuppressesShortBodiesAsync()
    {
        var test = new VerifyIdenticalBranches.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Ternary(bool flag) => flag ? 4 : 4;

                           public void Short(bool flag)
                           {
                               if (flag)
                               {
                                   Run();
                               }
                               else
                               {
                                   Run();
                               }
                           }

                           public void Long(bool flag)
                           {
                               {|SST1476:if|} (flag)
                               {
                                   Run();
                                   Run();
                               }
                               else
                               {
                                   Run();
                                   Run();
                               }
                           }

                           private static void Run()
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1476.minimum_statements = 2

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide minimum applies when no rule-specific key is set.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GeneralMinimumAppliesAsync()
    {
        var test = new VerifyIdenticalBranches.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Ternary(bool flag) => flag ? 4 : 4;

                           public void Short(bool flag)
                           {
                               if (flag)
                               {
                                   Run();
                               }
                               else
                               {
                                   Run();
                               }
                           }

                           private static void Run()
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.minimum_statements = 2

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable minimum falls back to the default rather than silencing the rule.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnparsableMinimumFallsBackToTheDefaultAsync()
    {
        var test = new VerifyIdenticalBranches.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Pick(bool flag)
                           {
                               {|SST1476:if|} (flag)
                               {
                                   return 1;
                               }
                               else
                               {
                                   return 1;
                               }
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1476.minimum_statements = several

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
