// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCompound = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.UseCompoundAssignmentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1185 (use a compound assignment) and its fix.</summary>
public class UseCompoundAssignmentAnalyzerUnitTest
{
    /// <summary>Verifies <c>x = x + y</c> is reported and folded into <c>x += y</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddRecomputeFoldedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(int count)
                                  {
                                      {|SST1185:count = count + 1|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(int count)
                                       {
                                           count += 1;
                                       }
                                   }
                                   """;
        await VerifyCompound.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member-access target is folded and an unrelated assignment is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberTargetFoldedAndOthersCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _total;

                                  public void M(int x, int y)
                                  {
                                      {|SST1185:this._total = this._total * 2|};
                                      x = y + 1;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _total;

                                       public void M(int x, int y)
                                       {
                                           this._total *= 2;
                                           x = y + 1;
                                       }
                                   }
                                   """;
        await VerifyCompound.VerifyCodeFixAsync(Source, FixedSource);
    }
}
