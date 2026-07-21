// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyChain = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1529NullConditionalNewLineAnalyzer,
    StyleSharp.Analyzers.Sst1529NullConditionalNewLineCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the wrapped call-chain operator placement rule (SST1529).</summary>
public class CallChainNewLineUnitTest
{
    /// <summary>Verifies a trailing '.' link is reported and moved to lead the continuation line by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingMemberAccessMovedToLeadByDefaultAsync()
    {
        const string config = """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1529.severity = warning

            """;
        var test = new VerifyChain.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static void M(string s)
                           {
                               _ = s{|SST1529:.|}
                                   Trim();
                           }
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static void M(string s)
                            {
                                _ = s
                                    .Trim();
                            }
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a trailing '?.' link is reported and moved to lead the continuation line, keeping '?.' intact.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingConditionalAccessMovedToLeadAsync()
    {
        const string config = """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1529.severity = warning

            """;
        var test = new VerifyChain.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static void M(string s)
                           {
                               _ = s{|SST1529:?|}.
                                   Trim();
                           }
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static void M(string s)
                            {
                                _ = s
                                    ?.Trim();
                            }
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a leading-operator chain and a single-line chain are clean under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingChainAndSingleLineAreCleanAsync()
    {
        var test = new VerifyChain.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static string M(string s)
                           {
                               var single = s.Trim();
                               return single
                                   .Trim()
                                   .ToUpperInvariant();
                           }
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1529.severity = warning

            """));
        await test.RunAsync(CancellationToken.None);
    }
}
