// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyParameterLayout = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ParameterListLayoutAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the parameter/argument layout rules (SST1110–SST1115, SST1118).</summary>
public class ParameterListLayoutAnalyzerUnitTest
{
    /// <summary>Verifies an opening parenthesis off the declaration line is reported (SST1110).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OpeningParenOffDeclarationLineReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M
                    {|SST1110:(|}int x) => _ = x;
            }
            """);

    /// <summary>Verifies a closing parenthesis off the last parameter's line is reported (SST1111).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClosingParenOffLastParameterLineReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(
                    int x
                {|SST1111:)|} => _ = x;
            }
            """);

    /// <summary>Verifies an empty list's closing parenthesis on another line is reported (SST1112).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyListClosingParenOnOtherLineReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(
                {|SST1112:)|}
                {
                }
            }
            """);

    /// <summary>Verifies a comma off the previous parameter's line is reported (SST1113).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommaOffPreviousParameterLineReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x
                    {|SST1113:,|} int y) => _ = x + y;
            }
            """);

    /// <summary>Verifies a blank line before the first parameter is reported (SST1114).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBeforeFirstParameterReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(

                    {|SST1114:int x|}) => _ = x;
            }
            """);

    /// <summary>Verifies a blank line before a later parameter is reported (SST1115).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBeforeLaterParameterReportedAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x,

                    {|SST1115:int y|}) => _ = x + y;
            }
            """);

    /// <summary>Verifies a multi-line argument is reported (SST1118, opt-in) and exempt callbacks are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineArgumentReportedButCallbacksExemptAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq;

            internal class C
            {
                private static int Add(int value) => value;

                private static void M()
                {
                    Add({|SST1118:1 +
                        2|});
                    Run(() =>
                    {
                        _ = 1;
                    });
                }

                private static void Run(Action action) => action();
            }
            """);

    /// <summary>Verifies a single-line parameter list is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineListIsCleanAsync()
        => await VerifyParameterLayout.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M(int x, int y) => _ = x + y;
            }
            """);
}
