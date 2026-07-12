// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDuplicateCondition = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1475DuplicateConditionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1475 (a condition should not be repeated in an if/else-if chain).</summary>
public class DuplicateConditionAnalyzerUnitTest
{
    /// <summary>Verifies a condition repeated later in the same chain is reported on the unreachable branch.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedConditionInChainIsReportedAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Classify(int value)
                {
                    if (value < 0)
                    {
                        return -1;
                    }
                    else if (value > 10)
                    {
                        return 2;
                    }
                    else if ({|SST1475:value < 0|})
                    {
                        return 3;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies every later copy of a condition is reported, not just the first.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EveryLaterCopyIsReportedAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Classify(int value)
                {
                    if (value < 0)
                    {
                        return -1;
                    }
                    else if ({|SST1475:value < 0|})
                    {
                        return 2;
                    }
                    else if ({|SST1475:value < 0|})
                    {
                        return 3;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a long chain is analyzed once, so one duplicate produces exactly one diagnostic.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Only the head of a chain is analyzed. Were each <c>else if</c> analyzed as a chain of its own, this
    /// five-branch chain would report its duplicate several times over.
    /// </remarks>
    [Test]
    public async Task LongChainIsAnalyzedOnceAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Classify(int value)
                {
                    if (value < 0)
                    {
                        return -1;
                    }
                    else if (value == 0)
                    {
                        return 0;
                    }
                    else if (value < 10)
                    {
                        return 1;
                    }
                    else if (value < 100)
                    {
                        return 2;
                    }
                    else if ({|SST1475:value == 0|})
                    {
                        return 3;
                    }

                    return 4;
                }
            }
            """);

    /// <summary>Verifies a chain whose conditions all differ is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DistinctConditionsAreCleanAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Classify(int value)
                {
                    if (value < 0)
                    {
                        return -1;
                    }
                    else if (value == 0)
                    {
                        return 0;
                    }
                    else if (value > 10)
                    {
                        return 2;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }
            """);

    /// <summary>Verifies formatting differences do not hide a repeated condition.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TriviaDoesNotHideARepeatedConditionAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Classify(int value)
                {
                    if (value == 0)
                    {
                        return 1;
                    }
                    else if ({|SST1475:value   ==   0|})
                    {
                        return 2;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a nested if is a chain of its own, so it may repeat the condition that guards it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Redundant, but reachable: the inner branch does run. Only a repeat within one chain is unreachable.</remarks>
    [Test]
    public async Task NestedIfIsItsOwnChainAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Classify(int value)
                {
                    if (value < 0)
                    {
                        if (value < 0)
                        {
                            return 1;
                        }
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a repeated condition that calls a method is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The second call may legitimately answer differently, so the branch is not provably unreachable.</remarks>
    [Test]
    public async Task RepeatedInvocationIsCleanAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Check(int value) => value > 0;

                public int Classify(int value)
                {
                    if (Check(value))
                    {
                        return 1;
                    }
                    else if (Check(value))
                    {
                        return 2;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a repeated condition that mutates, assigns, or allocates is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedSideEffectingConditionIsCleanAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _count;

                public int Increment(int value)
                {
                    if (_count++ > value)
                    {
                        return 1;
                    }
                    else if (_count++ > value)
                    {
                        return 2;
                    }

                    return 0;
                }

                public int Assign(int value)
                {
                    var current = 0;
                    if ((current = value) > 0)
                    {
                        return 1;
                    }
                    else if ((current = value) > 0)
                    {
                        return current;
                    }

                    return 0;
                }

                public int Allocate(int value)
                {
                    if (new int[1].Length > value)
                    {
                        return 1;
                    }
                    else if (new int[1].Length > value)
                    {
                        return 2;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a repeated awaited condition is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedAwaitedConditionIsCleanAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task<int> ClassifyAsync()
                {
                    if (await GetAsync())
                    {
                        return 1;
                    }
                    else if (await GetAsync())
                    {
                        return 2;
                    }

                    return 0;
                }

                private static Task<bool> GetAsync() => Task.FromResult(true);
            }
            """);

    /// <summary>Verifies a repeated case label in a switch statement is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedSwitchLabelIsReportedAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Route(int value, bool flag)
                {
                    switch (value)
                    {
                        case 1 when flag:
                            return 10;
                        case 2:
                            return 20;
                        {|SST1475:case 1 when flag:|}
                            return 30;
                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a switch whose labels all differ is clean, including a guarded and an unguarded constant.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks><c>case 1 when flag:</c> and <c>case 1:</c> test different things; only the second can be reached without the guard.</remarks>
    [Test]
    public async Task DistinctSwitchLabelsAreCleanAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Route(int value, bool flag)
                {
                    switch (value)
                    {
                        case 1 when flag:
                            return 10;
                        case 1:
                            return 11;
                        case 2:
                            return 20;
                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a repeated label whose guard calls a method is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedSwitchLabelWithImpureGuardIsCleanAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Check(int value) => value > 0;

                public int Route(int value)
                {
                    switch (value)
                    {
                        case 1 when Check(value):
                            return 10;
                        case 1 when Check(value):
                            return 20;
                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a repeated arm in a switch expression is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedSwitchExpressionArmIsReportedAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name(int value, bool flag) => value switch
                {
                    1 when flag => "one",
                    2 => "two",
                    {|SST1475:1 when flag|} => "uno",
                    _ => "other",
                };
            }
            """);

    /// <summary>Verifies a switch expression whose arms all differ is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DistinctSwitchExpressionArmsAreCleanAsync()
        => await VerifyDuplicateCondition.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Name(int value, bool flag) => value switch
                {
                    1 when flag => "one",
                    1 => "uno",
                    2 => "two",
                    _ => "other",
                };
            }
            """);
}
