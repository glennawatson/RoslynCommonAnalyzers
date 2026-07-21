// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1663SummaryCommentAnalyzer,
    StyleSharp.Analyzers.Sst1663SummaryCommentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1663 (a summary-like comment should be a documentation comment).</summary>
public class SummaryCommentAnalyzerUnitTest
{
    /// <summary>Verifies a comment separated from the member by a blank line is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineSeparatedCommentIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                // Not a summary

                public int Count { get; }
            }
            """);

    /// <summary>Verifies a stacked multi-line comment block is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StackedCommentBlockIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                // First line
                // Second line
                public int Count { get; }
            }
            """);

    /// <summary>Verifies a comment above a non-public member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPublicMemberIsIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                // Gets the count
                private int Count { get; }
            }
            """);

    /// <summary>Verifies a summary-like comment above a public member is converted to documentation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SummaryLikeCommentIsConvertedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1663:// Gets the widget count|}
                                  public int Count { get; }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Gets the widget count</summary>
                                       public int Count { get; }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies XML-significant characters in the comment are escaped during conversion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpecialCharactersAreEscapedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1663:// Uses a & b < c|}
                                  public int Value { get; }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Uses a &amp; b &lt; c</summary>
                                       public int Value { get; }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
