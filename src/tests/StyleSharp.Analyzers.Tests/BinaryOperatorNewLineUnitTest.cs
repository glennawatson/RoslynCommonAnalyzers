// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBinary = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1526BinaryOperatorNewLineAnalyzer,
    StyleSharp.Analyzers.TokenLineBreakCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the wrapped binary operator placement rule (SST1526).</summary>
public class BinaryOperatorNewLineUnitTest
{
    /// <summary>Verifies a trailing operator is reported and moved to lead the continuation line by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingOperatorMovedToLeadByDefaultAsync()
    {
        var test = new VerifyBinary.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static int M(int a, int b)
                           {
                               return a {|SST1526:+|}
                                   b;
                           }
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static int M(int a, int b)
                            {
                                return a
                                    + b;
                            }
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1526.severity = warning

            """));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1526.severity = warning

            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a leading operator is reported and moved to trail the upper line when 'after' is configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingOperatorMovedToTrailWhenAfterConfiguredAsync()
    {
        var test = new VerifyBinary.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static int M(int a, int b)
                           {
                               return a
                                   {|SST1526:+|} b;
                           }
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static int M(int a, int b)
                            {
                                return a +
                                    b;
                            }
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1526.severity = warning
            stylesharp.binary_operator_new_line = after

            """));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1526.severity = warning
            stylesharp.binary_operator_new_line = after

            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a leading operator and a single-line expression are clean under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingAndSingleLineAreCleanAsync()
    {
        var test = new VerifyBinary.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static int M(int a, int b)
                           {
                               var single = a + b;
                               return a
                                   + b + single;
                           }
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1526.severity = warning

            """));
        await test.RunAsync(CancellationToken.None);
    }
}
