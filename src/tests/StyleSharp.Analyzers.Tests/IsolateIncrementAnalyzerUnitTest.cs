// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyIncrement = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2015IsolateIncrementAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2015 (do not bury an increment inside a larger expression).</summary>
public class IsolateIncrementAnalyzerUnitTest
{
    /// <summary>Verifies an increment whose value a larger expression reads is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BuriedIncrementIsReportedAsync()
        => await VerifyIncrement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Index(int[] values, int i)
                {
                    values[{|SST2015:i++|}] = Compute(i);
                }

                public int Arithmetic(int x, int y) => Compute({|SST2015:x++|} + y);

                public int Argument(int x) => Compute({|SST2015:x++|});

                public int Compound(int x, int total)
                {
                    total += {|SST2015:x++|};
                    return total;
                }

                public int Chained(int x) => Compute({|SST2015:++x|}) + Compute({|SST2015:x--|});

                private static int Compute(int value) => value;
            }
            """);

    /// <summary>Verifies an increment that is the whole expression is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StandaloneIncrementIsCleanAsync()
        => await VerifyIncrement.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _next;

                public int Next() => _next++;

                public int Take()
                {
                    return _next++;
                }

                public void Count(int[] values)
                {
                    var i = 0;
                    i++;
                    --i;

                    for (var j = 0; j < values.Length; j++)
                    {
                        var captured = j++;
                        _next = captured++;
                    }
                }
            }
            """);

    /// <summary>Verifies the message names the increment and the variable it writes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MessageNamesTheIncrementAndItsTargetAsync()
    {
        var test = new VerifyIncrement.Test
        {
            TestCode = """
                       public class C
                       {
                           public int M(int[] values, int i) => values[i++];
                       }
                       """,
        };

        test.ExpectedDiagnostics.Add(VerifyIncrement.Diagnostic().WithSpan(3, 49, 3, 52).WithArguments("i++", "i"));
        await test.RunAsync(CancellationToken.None);
    }
}
