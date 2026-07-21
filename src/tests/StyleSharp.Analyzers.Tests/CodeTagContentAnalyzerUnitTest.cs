// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1661CodeTagContentAnalyzer,
    StyleSharp.Analyzers.Sst1661CodeTagContentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1661 (documentation code tag should match its content).</summary>
public class CodeTagContentAnalyzerUnitTest
{
    /// <summary>Verifies inline single-line <c>&lt;c&gt;</c> and a multi-line <c>&lt;code&gt;</c> are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchingTagsAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>
                /// Inline <c>ok</c> and a block:
                /// <code>
                /// line1
                /// line2
                /// </code>
                /// </summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a single-line snippet in <c>&lt;code&gt;</c> is switched to <c>&lt;c&gt;</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineCodeBecomesInlineAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Value {|SST1661:<code>x = 1</code>|}.</summary>
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Value <c>x = 1</c>.</summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-line snippet in <c>&lt;c&gt;</c> is switched to <c>&lt;code&gt;</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineInlineBecomesBlockAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>
                                  /// Example {|SST1661:<c>a
                                  /// b</c>|}.
                                  /// </summary>
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>
                                       /// Example <code>a
                                       /// b</code>.
                                       /// </summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
