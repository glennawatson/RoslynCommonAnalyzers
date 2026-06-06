// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCommentContent = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.CommentContentAnalyzer,
    StyleSharp.Analyzers.CommentContentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the empty-comment rule (SST1120).</summary>
public class CommentContentAnalyzerUnitTest
{
    /// <summary>Verifies an empty comment alone on a line is reported (SST1120) and the line removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyCommentOnOwnLineRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1120://|}
                                  private static void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyCommentContent.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty comment trailing code is reported (SST1120) and removed with its leading space.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyTrailingCommentRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static void M()
                                  {
                                      var x = 1; {|SST1120://|}
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
        await VerifyCommentContent.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies comments with text and commented-out code markers are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEmptyCommentsAreCleanAsync()
        => await VerifyCommentContent.VerifyAnalyzerAsync(
            """
            internal class C
            {
                // explains the field
                //// var disabled = 1;
                private int field;
            }
            """);
}
