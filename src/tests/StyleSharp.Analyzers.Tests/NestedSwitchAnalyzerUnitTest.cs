// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNestedSwitch = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2252NestedSwitchAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2252 (a switch statement nested inside another switch statement's section).</summary>
public class NestedSwitchAnalyzerUnitTest
{
    /// <summary>Verifies a switch statement in a section of another switch statement is reported on its keyword.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchStatementNestedInASectionIsReportedAsync()
        => await VerifyNestedSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int outer, int inner)
                {
                    switch (outer)
                    {
                        case 1:
                            {|SST2252:switch|} (inner)
                            {
                                case 10:
                                    return 100;
                                default:
                                    return 0;
                            }

                        default:
                            return -1;
                    }
                }
            }
            """);

    /// <summary>Verifies each inner switch in a three-deep nest is reported, once per enclosing switch found.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EachSwitchNestedInsideAnotherIsReportedAsync()
        => await VerifyNestedSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int a, int b, int c)
                {
                    switch (a)
                    {
                        case 1:
                            {|SST2252:switch|} (b)
                            {
                                case 2:
                                    {|SST2252:switch|} (c)
                                    {
                                        case 3:
                                            return 3;
                                        default:
                                            return 0;
                                    }

                                default:
                                    return -2;
                            }

                        default:
                            return -1;
                    }
                }
            }
            """);

    /// <summary>Verifies two switch statements side by side in one method are not nesting and are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SiblingSwitchStatementsAreNotReportedAsync()
        => await VerifyNestedSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int a, int b)
                {
                    int x;
                    switch (a)
                    {
                        case 1:
                            x = 1;
                            break;
                        default:
                            x = 0;
                            break;
                    }

                    switch (b)
                    {
                        case 2:
                            x += 2;
                            break;
                        default:
                            break;
                    }

                    return x;
                }
            }
            """);

    /// <summary>Verifies a switch inside a lambda declared in a section belongs to the lambda, not the outer switch.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchInsideALambdaInASectionIsNotReportedAsync()
        => await VerifyNestedSwitch.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public int M(int outer, int inner)
                {
                    switch (outer)
                    {
                        case 1:
                            Func<int, int> pick = value =>
                            {
                                switch (value)
                                {
                                    case 10:
                                        return 100;
                                    default:
                                        return 0;
                                }
                            };
                            return pick(inner);

                        default:
                            return -1;
                    }
                }
            }
            """);

    /// <summary>Verifies a switch inside a local function declared in a section belongs to that function.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchInsideALocalFunctionInASectionIsNotReportedAsync()
        => await VerifyNestedSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int outer, int inner)
                {
                    switch (outer)
                    {
                        case 1:
                            int Local(int value)
                            {
                                switch (value)
                                {
                                    case 10:
                                        return 100;
                                    default:
                                        return 0;
                                }
                            }

                            return Local(inner);

                        default:
                            return -1;
                    }
                }
            }
            """);

    /// <summary>Verifies a switch expression inside a switch statement's section is left alone as the preferred form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchExpressionInsideASectionIsNotReportedAsync()
        => await VerifyNestedSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int outer, int inner)
                {
                    switch (outer)
                    {
                        case 1:
                            return inner switch
                            {
                                10 => 100,
                                _ => 0,
                            };

                        default:
                            return -1;
                    }
                }
            }
            """);

    /// <summary>Verifies a single switch statement with no enclosing switch is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoneSwitchStatementIsNotReportedAsync()
        => await VerifyNestedSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 1:
                            return 1;
                        default:
                            return 0;
                    }
                }
            }
            """);
}
