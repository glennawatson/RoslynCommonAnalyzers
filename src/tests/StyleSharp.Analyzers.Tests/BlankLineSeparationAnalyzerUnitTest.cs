// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.BlankLineSeparationAnalyzer,
    StyleSharp.Analyzers.BlankLineSeparationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the blank-line separation rules (SST1534, SST1535, SST1536, SST1537).</summary>
public class BlankLineSeparationAnalyzerUnitTest
{
    /// <summary>Verifies a statement crowding a multi-line block's closing brace is reported and spaced out.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StatementCrowdingBlockReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool flag)
                                  {
                                      if (flag)
                                      {
                                          return 1;
                                      }
                                      {|SST1534:return|} 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(bool flag)
                                       {
                                           if (flag)
                                           {
                                               return 1;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line after a multi-line block satisfies the rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatedBlockIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(bool flag)
                {
                    if (flag)
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a single-line block is left alone, since no closing brace ends a line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineBlockIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(bool flag)
                {
                    if (flag) { return 1; }
                    return 0;
                }
            }
            """);

    /// <summary>Verifies a blank line after a constructor initializer's colon is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineAfterConstructorInitializerColonReportedAsync()
    {
        const string Source = """
                              public class Base
                              {
                                  public Base(int value)
                                  {
                                  }
                              }

                              public sealed class C : Base
                              {
                                  public C()
                                      {|SST1535::|}

                                      base(1)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class Base
                                   {
                                       public Base(int value)
                                       {
                                       }
                                   }

                                   public sealed class C : Base
                                   {
                                       public C()
                                           :
                                           base(1)
                                       {
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line after a conditional operator's '?' is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineAfterConditionalQuestionReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool flag) => flag
                                      {|SST1536:?|}

                                      1
                                      : 0;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(bool flag) => flag
                                           ?
                                           1
                                           : 0;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line after an expression-body arrow is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineAfterArrowReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                      {|SST1537:=>|}

                                      42;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M()
                                           =>
                                           42;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a wrapped expression body without a gap is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrappedArrowWithoutGapIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M()
                    => 42;
            }
            """);

    /// <summary>Verifies a comment between the token and its continuation is content, not a gap.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentAfterArrowIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M()
                    =>
                    // The answer.
                    42;
            }
            """);
}
