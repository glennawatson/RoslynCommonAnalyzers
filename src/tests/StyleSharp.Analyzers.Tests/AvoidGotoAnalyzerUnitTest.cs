// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyGoto = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2014AvoidGotoAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2014 (avoid goto).</summary>
public class AvoidGotoAnalyzerUnitTest
{
    /// <summary>Verifies a jump to a label is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JumpToALabelIsReportedAsync()
        => await VerifyGoto.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        if (values[i] < 0)
                        {
                            {|SST2014:goto Failed;|}
                        }
                    }

                    return 0;

                Failed:
                    return -1;
                }
            }
            """);

    /// <summary>Verifies a jump between switch sections is not reported: the language has no other word for it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JumpBetweenSwitchSectionsIsCleanAsync()
        => await VerifyGoto.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int state)
                {
                    switch (state)
                    {
                        case 1:
                            System.Console.WriteLine(1);
                            goto case 2;

                        case 2:
                            System.Console.WriteLine(2);
                            goto default;

                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies the structured jumps are never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructuredJumpsAreCleanAsync()
        => await VerifyGoto.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    foreach (var value in values)
                    {
                        if (value == 0)
                        {
                            continue;
                        }

                        if (value < 0)
                        {
                            break;
                        }

                        return value;
                    }

                    return 0;
                }
            }
            """);
}
