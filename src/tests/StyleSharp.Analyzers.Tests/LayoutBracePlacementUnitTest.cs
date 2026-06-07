// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBlank = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.BracePlacementAnalyzer,
    StyleSharp.Analyzers.BlankLineRemovalCodeFixProvider>;
using VerifyBrace = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.BracePlacementAnalyzer,
    StyleSharp.Analyzers.BracePlacementCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the brace-placement rules (SST1500/SST1505/SST1508/SST1509).</summary>
public class LayoutBracePlacementUnitTest
{
    /// <summary>Verifies a well-formatted Allman block produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllmanBlockIsCleanAsync()
        => await VerifyBrace.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """);

    /// <summary>Verifies a single-line auto-property accessor list is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineAccessorListIsCleanAsync()
        => await VerifyBrace.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int X { get; set; }
            }
            """);

    /// <summary>Verifies a property with an initializer below a blank line is not flagged (the '{' is mid-line).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyInitializerBelowBlankLineIsCleanAsync()
        => await VerifyBrace.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int _a;

                public int X { get; set; } = 0;
            }
            """);

    /// <summary>Verifies a brace sharing its line with earlier code is reported (SST1500) and moved to its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedLineBraceMovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M() {|SST1500:{|}
                    return;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """;
        await VerifyBrace.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line before an opening brace is reported (SST1509) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBeforeOpenBraceRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M()

                {|SST1509:{|}
                    return;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """;
        await VerifyBlank.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line after an opening brace is reported (SST1505) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineAfterOpenBraceRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M()
                {|SST1505:{|}

                    return;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """;
        await VerifyBlank.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line before a closing brace is reported (SST1508) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBeforeCloseBraceRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M()
                {
                    return;

                {|SST1508:}|}
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """;
        await VerifyBlank.VerifyCodeFixAsync(Source, FixedSource);
    }
}
