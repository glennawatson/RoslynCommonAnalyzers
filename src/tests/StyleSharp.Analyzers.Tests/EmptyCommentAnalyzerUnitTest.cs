// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyComment = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1659EmptyCommentAnalyzer,
    StyleSharp.Analyzers.Sst1659EmptyCommentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1659 (documentation comments should not be empty).</summary>
public class EmptyCommentAnalyzerUnitTest
{
    /// <summary>Verifies an empty ordinary comment belongs to SST1120 and is not reported here as well.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// SST1120 already reports an empty <c>//</c> or <c>/* */</c>. Reporting them here too would put two
    /// squiggles on one comment and ask the reader to dismiss the same finding twice, so this rule sees only
    /// documentation comments. This test is the boundary between the two.
    /// </remarks>
    [Test]
    public async Task EmptyOrdinaryCommentsBelongToTheOtherRuleAsync()
        => await VerifyEmptyComment.VerifyAnalyzerAsync(
            """
            internal class C
            {
                //

                //

                /* */
                public int Value;
            }
            """);

    /// <summary>Verifies a documentation comment that is nothing but its marker is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyDocumentationCommentIsRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1659:///|}
                                  public int Value;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int Value;
                                   }
                                   """;
        await VerifyEmptyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies several blank documentation lines are one empty comment, and all of them go.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyDocumentationCommentSpanningLinesIsRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1659:///
                                  ///|}
                                  public int Value;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int Value;
                                   }
                                   """;
        await VerifyEmptyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty block documentation comment is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyBlockDocumentationCommentIsRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1659:/** */|}
                                  public int Value;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int Value;
                                   }
                                   """;
        await VerifyEmptyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line inside a documentation comment that says something is ordinary formatting.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineInsideDocumentationCommentIsCleanAsync()
        => await VerifyEmptyComment.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>The value.</summary>
                ///
                /// <remarks>It is a number.</remarks>
                public int Value;
            }
            """);

    /// <summary>Verifies the commented-out code marker keeps its slashes as text and is never empty.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentedOutCodeMarkerIsCleanAsync()
        => await VerifyEmptyComment.VerifyAnalyzerAsync(
            """
            internal class C
            {
                //// public int Disabled;
                public int Value;
            }
            """);

    /// <summary>Verifies comments with text, and preprocessor directives, are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentsWithTextAndDirectivesAreCleanAsync()
        => await VerifyEmptyComment.VerifyAnalyzerAsync(
            """
            #region Values
            internal class C
            {
                // the running total
                public int Value; /* counted */

                /// <summary>Adds one.</summary>
                /// <returns>The new total.</returns>
                public int Increment() => ++Value;
            }
            #endregion
            """);

    /// <summary>Verifies an empty comment inside a region is removed without disturbing the region.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A directive is its own trivia and never a comment, so it is never reported; and the removal only ever
    /// takes lines the comment had to itself, which a directive's line is not.
    /// </remarks>
    [Test]
    public async Task EmptyCommentInsideARegionKeepsTheRegionAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  #region Values
                                  {|SST1659:///|}
                                  public int Value;
                                  #endregion
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       #region Values
                                       public int Value;
                                       #endregion
                                   }
                                   """;
        await VerifyEmptyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every empty comment in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRemovesEveryEmptyCommentAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1659:///|}
                                  public int Value;

                                  {|SST1659:///
                                  ///|}
                                  public int Run()
                                  {
                                      var total = Value;
                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int Value;

                                       public int Run()
                                       {
                                           var total = Value;
                                           return total;
                                       }
                                   }
                                   """;
        await VerifyEmptyComment.VerifyCodeFixAsync(Source, FixedSource);
    }
}
