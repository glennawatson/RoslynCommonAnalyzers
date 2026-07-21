// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1664SummaryParagraphAnalyzer,
    StyleSharp.Analyzers.Sst1664SummaryParagraphCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1664 (summary paragraphs should use para elements).</summary>
public class SummaryParagraphAnalyzerUnitTest
{
    /// <summary>Verifies a single-paragraph multi-line summary is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleParagraphIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>
                /// Just one paragraph here.
                /// </summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a summary that already contains an element is out of scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SummaryWithNestedElementIsIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>
                /// First paragraph with <see cref="C"/>.
                ///
                /// Second paragraph.
                /// </summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies blank-line-separated paragraphs are wrapped in <c>&lt;para&gt;</c> elements.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankSeparatedParagraphsAreWrappedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// {|SST1664:<summary>
                                  /// First paragraph.
                                  ///
                                  /// Second paragraph.
                                  /// </summary>|}
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>
                                       /// <para>
                                       /// First paragraph.
                                       /// </para>
                                       /// <para>
                                       /// Second paragraph.
                                       /// </para>
                                       /// </summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
