// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyIndentationFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1137ElementIndentationAnalyzer,
    StyleSharp.Analyzers.Sst1137ElementIndentationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1137 code fix (elements should have the same indentation).</summary>
public class Sst1137ElementIndentationCodeFixUnitTest
{
    /// <summary>Verifies an over-indented member is pulled back to its siblings' column.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverIndentedMemberIsPulledBackAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int First => 1;

                                      {|SST1137:public|} int Second => 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int First => 1;

                                       public int Second => 2;
                                   }
                                   """;
        await VerifyIndentationFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-line member keeps its inner shape when it moves.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MultiLineMemberKeepsItsShapeAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int First => 1;

                                      {|SST1137:public|} int Second()
                                      {
                                          return 2;
                                      }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int First => 1;

                                       public int Second()
                                       {
                                           return 2;
                                       }
                                   }
                                   """;
        await VerifyIndentationFix.VerifyCodeFixAsync(Source, FixedSource);
    }
}
