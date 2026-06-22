// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyComplexity = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FunctionComplexityAnalyzer>;
using VerifySingleIterationLoop = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.SingleIterationLoopAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for function-complexity maintainability rules.</summary>
public class ComplexityAnalyzerUnitTest
{
    /// <summary>Verifies SST1442 reports methods over the default branching-complexity threshold.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CyclomaticComplexityOverDefaultThresholdIsReportedAsync()
        => await VerifyComplexity.VerifyAnalyzerAsync(
            """
            public class C
            {
                {|SST1442:public int M(int value)
                {
                    var result = 0;
                    if (value == 0) result++;
                    if (value == 1) result++;
                    if (value == 2) result++;
                    if (value == 3) result++;
                    if (value == 4) result++;
                    if (value == 5) result++;
                    if (value == 6) result++;
                    if (value == 7) result++;
                    if (value == 8) result++;
                    if (value == 9) result++;
                    return result;
                }|}
            }
            """);

    /// <summary>Verifies the SST1442 threshold can be raised per tree.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CyclomaticComplexityThresholdIsConfigurableAsync()
    {
        var test = new VerifyComplexity.Test
        {
            TestCode = """
                       public class C
                       {
                           public int M(int value)
                           {
                               var result = 0;
                               if (value == 0) result++;
                               if (value == 1) result++;
                               if (value == 2) result++;
                               if (value == 3) result++;
                               if (value == 4) result++;
                               if (value == 5) result++;
                               if (value == 6) result++;
                               if (value == 7) result++;
                               if (value == 8) result++;
                               if (value == 9) result++;
                               return result;
                           }
                       }
                       """
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1442.max_cyclomatic_complexity = 11

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies wide switch expressions do not inflate complexity.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WideSwitchExpressionIsCleanAsync()
        => await VerifyComplexity.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value) =>
                    value switch
                    {
                        0 => 0,
                        1 => 1,
                        2 => 2,
                        3 => 3,
                        4 => 4,
                        5 => 5,
                        6 => 6,
                        7 => 7,
                        8 => 8,
                        9 => 9,
                        10 => 10,
                        11 => 11,
                        _ => -1
                    };
            }
            """);

    /// <summary>Verifies wide switch statements do not inflate complexity.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WideSwitchStatementIsCleanAsync()
        => await VerifyComplexity.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 0: return 0;
                        case 1: return 1;
                        case 2: return 2;
                        case 3: return 3;
                        case 4: return 4;
                        case 5: return 5;
                        case 6: return 6;
                        case 7: return 7;
                        case 8: return 8;
                        case 9: return 9;
                        case 10: return 10;
                        case 11: return 11;
                        default: return -1;
                    }
                }
            }
            """);

    /// <summary>Verifies SST1443 reports methods over the default nested-flow threshold.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CognitiveComplexityOverDefaultThresholdIsReportedAsync()
        => await VerifyComplexity.VerifyAnalyzerAsync(
            """
            public class C
            {
                {|SST1443:public int M(int value)
                {
                    var result = 0;
                    if (value > 0)
                    {
                        if (value > 1)
                        {
                            if (value > 2)
                            {
                                if (value > 3)
                                {
                                    if (value > 4)
                                    {
                                        if (value > 5)
                                        {
                                            result = value;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return result;
                }|}
            }
            """);

    /// <summary>Verifies SST1444 reports an unconditional terminating jump in a loop.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SingleIterationLoopWithUnconditionalReturnIsReportedAsync()
        => await VerifySingleIterationLoop.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    {|SST1444:foreach (var value in values)
                    {
                        return value;
                    }|}

                    return 0;
                }
            }
            """);

    /// <summary>Verifies SST1444 ignores conditional continues and does not report the outer loop for nested-loop jumps.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConditionalContinueAndNestedLoopJumpsDoNotReportOuterLoopAsync()
        => await VerifySingleIterationLoop.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    foreach (var value in values)
                    {
                        if (value < 0)
                        {
                            continue;
                        }

                        {|SST1444:while (value > 0)
                        {
                            break;
                        }|}

                        return value;
                    }

                    return 0;
                }
            }
            """);
}
