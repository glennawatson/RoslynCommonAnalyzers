// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBlanks = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MultipleBlankLinesAnalyzer,
    StyleSharp.Analyzers.MultipleBlankLinesCodeFixProvider>;
using VerifySpacing = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ElementSpacingAnalyzer,
    StyleSharp.Analyzers.ElementSpacingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the blank-line layout rules (SST1507/SST1516).</summary>
public class LayoutBlankLineUnitTest
{
    /// <summary>Verifies two consecutive blank lines are reported (SST1507) and collapsed to one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleBlankLinesCollapsedAsync()
    {
        const string Source = """
            internal class C
            {
                private int a;


                private int b;
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private int a;

                private int b;
            }
            """;
        await VerifyBlanks.VerifyCodeFixAsync(Source, VerifyBlanks.Diagnostic("SST1507").WithSpan(5, 1, 6, 1), FixedSource);
    }

    /// <summary>Verifies a single blank line between members is allowed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleBlankLineIsCleanAsync()
        => await VerifyBlanks.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int a;

                private int b;
            }
            """);

    /// <summary>Verifies adjacent members without a blank line are reported (SST1516) and separated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdjacentMembersSeparatedAsync()
    {
        const string Source = """
            internal class C
            {
                private void A()
                {
                }
                {|SST1516:private|} void B()
                {
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void A()
                {
                }

                private void B()
                {
                }
            }
            """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies members already separated by a blank line are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatedMembersAreCleanAsync()
        => await VerifySpacing.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void A()
                {
                }

                private void B()
                {
                }
            }
            """);
}
