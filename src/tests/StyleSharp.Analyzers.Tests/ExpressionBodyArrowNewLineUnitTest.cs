// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyArrow = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1527ArrowTokenNewLineAnalyzer,
    StyleSharp.Analyzers.TokenLineBreakCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the wrapped expression-body arrow placement rule (SST1527).</summary>
public class ExpressionBodyArrowNewLineUnitTest
{
    /// <summary>Verifies a leading arrow is reported and moved to trail the signature by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingArrowMovedToTrailByDefaultAsync()
    {
        const string config = """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1527.severity = warning

            """;
        var test = new VerifyArrow.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static int M(int x)
                               {|SST1527:=>|} x;
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static int M(int x) =>
                                x;
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a trailing arrow is reported and moved to lead the body when 'before' is configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingArrowMovedToLeadWhenBeforeConfiguredAsync()
    {
        const string config = """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1527.severity = warning
            stylesharp.arrow_token_new_line = before

            """;
        var test = new VerifyArrow.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static int M(int x) {|SST1527:=>|}
                               x;
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static int M(int x)
                                => x;
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a single-line expression body is clean under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineExpressionBodyIsCleanAsync()
    {
        var test = new VerifyArrow.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static int M(int x) => x;
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1527.severity = warning

            """));
        await test.RunAsync(CancellationToken.None);
    }
}
