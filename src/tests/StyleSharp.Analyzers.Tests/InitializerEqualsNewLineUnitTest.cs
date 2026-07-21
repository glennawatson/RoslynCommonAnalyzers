// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEquals = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1528EqualsTokenNewLineAnalyzer,
    StyleSharp.Analyzers.TokenLineBreakCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the wrapped initializer equals-sign placement rule (SST1528).</summary>
public class InitializerEqualsNewLineUnitTest
{
    /// <summary>Verifies a leading equals sign is reported and moved to trail the name by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingEqualsMovedToTrailByDefaultAsync()
    {
        const string config = """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1528.severity = warning

            """;
        var test = new VerifyEquals.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static readonly int Value
                               {|SST1528:=|} 3;
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static readonly int Value =
                                3;
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a local initializer's trailing equals sign moves to lead when 'before' is configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingEqualsMovedToLeadWhenBeforeConfiguredAsync()
    {
        const string config = """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1528.severity = warning
            stylesharp.equals_token_new_line = before

            """;
        var test = new VerifyEquals.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static int M()
                           {
                               var value {|SST1528:=|}
                                   3;
                               return value;
                           }
                       }
                       """,
            FixedCode = """
                        internal class C
                        {
                            private static int M()
                            {
                                var value
                                    = 3;
                                return value;
                            }
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a property initializer and a single-line field initializer are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyInitializerAndSingleLineAreCleanAsync()
    {
        var test = new VerifyEquals.Test
        {
            TestCode = """
                       internal class C
                       {
                           private static readonly int Value = 3;

                           public int P { get; }
                               = 5;
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            dotnet_diagnostic.SST1528.severity = warning

            """));
        await test.RunAsync(CancellationToken.None);
    }
}
