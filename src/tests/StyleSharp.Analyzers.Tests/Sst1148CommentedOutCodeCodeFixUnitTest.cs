// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCommentedCodeFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1148CommentedOutCodeAnalyzer,
    StyleSharp.Analyzers.Sst1148CommentedOutCodeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1148 code fix (remove commented-out code).</summary>
public class Sst1148CommentedOutCodeCodeFixUnitTest
{
    /// <summary>Verifies a comment that owns its line takes the line with it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CommentOnItsOwnLineTakesTheLineAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M()
                                  {
                                      {|SST1148:// return;|}
                                      System.Console.WriteLine("a");
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M()
                                       {
                                           System.Console.WriteLine("a");
                                       }
                                   }
                                   """;
        await VerifyCommentedCodeFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment trailing real code loses only itself.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CommentTrailingCodeKeepsTheCodeAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M()
                                  {
                                      System.Console.WriteLine("a"); {|SST1148:// return;|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M()
                                       {
                                           System.Console.WriteLine("a");
                                       }
                                   }
                                   """;
        await VerifyCommentedCodeFix.VerifyCodeFixAsync(Source, FixedSource);
    }
}
