// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyClose = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1513ClosingBraceSpacingAnalyzer,
    StyleSharp.Analyzers.Sst1513ClosingBraceSpacingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the closing-brace spacing rule (SST1513).</summary>
public class LayoutClosingBraceUnitTest
{
    /// <summary>Verifies a closing brace followed directly by a statement is reported (SST1513) and separated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CloseBraceFollowedByStatementSeparatedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void M(bool x)
                                  {
                                      if (x)
                                      {
                                      {|SST1513:}|}
                                      System.Console.WriteLine();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private void M(bool x)
                                       {
                                           if (x)
                                           {
                                           }

                                           System.Console.WriteLine();
                                       }
                                   }
                                   """;
        await VerifyClose.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a closing brace followed by another closing brace is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CloseBraceBeforeCloseBraceIsCleanAsync()
        => await VerifyClose.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a closing brace already followed by a blank line is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CloseBraceWithBlankLineIsCleanAsync()
        => await VerifyClose.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                    }

                    System.Console.WriteLine();
                }
            }
            """);
}
