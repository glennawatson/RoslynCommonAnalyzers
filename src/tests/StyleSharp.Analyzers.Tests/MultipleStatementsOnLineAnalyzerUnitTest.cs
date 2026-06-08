// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyMultipleStatements = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1107MultipleStatementsOnLineAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the multiple-statements-on-one-line rule (SST1107).</summary>
public class MultipleStatementsOnLineAnalyzerUnitTest
{
    /// <summary>Verifies a second statement sharing a line is reported (SST1107).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecondStatementOnSameLineReportedAsync()
        => await VerifyMultipleStatements.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x) => System.Console.WriteLine(x);

                private static void N()
                {
                    M(1); {|SST1107:M(2);|}
                }
            }
            """);

    /// <summary>Verifies a statement sharing a line inside a switch section is reported (SST1107).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchSectionSameLineReportedAsync()
        => await VerifyMultipleStatements.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x) => System.Console.WriteLine(x);

                private static void N(int value)
                {
                    switch (value)
                    {
                        case 1:
                            M(1); {|SST1107:M(2);|}
                            break;
                    }
                }
            }
            """);

    /// <summary>Verifies statements on separate lines are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateLinesAreCleanAsync()
        => await VerifyMultipleStatements.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x) => System.Console.WriteLine(x);

                private static void N()
                {
                    M(1);
                    M(2);
                }
            }
            """);
}
