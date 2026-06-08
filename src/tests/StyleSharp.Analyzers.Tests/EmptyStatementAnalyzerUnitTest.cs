// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyStatement = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1106EmptyStatementAnalyzer,
    StyleSharp.Analyzers.Sst1106EmptyStatementCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the empty-statement rule (SST1106).</summary>
public class EmptyStatementAnalyzerUnitTest
{
    /// <summary>Verifies a stray semicolon in a block is reported (SST1106) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StraySemicolonRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static void M()
                                  {
                                      var x = 1;
                                      {|SST1106:;|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static void M()
                                       {
                                           var x = 1;
                                       }
                                   }
                                   """;
        await VerifyEmptyStatement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a normal statement is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NormalStatementIsCleanAsync()
        => await VerifyEmptyStatement.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M()
                {
                    var x = 1;
                }
            }
            """);
}
