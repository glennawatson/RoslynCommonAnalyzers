// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRemoveCaseBesideDefault = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1466RemoveCaseBesideDefaultAnalyzer,
    StyleSharp.Analyzers.Sst1466RemoveCaseBesideDefaultCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1466 (remove case labels that share a section with default) and its fix.</summary>
public class RemoveCaseBesideDefaultAnalyzerUnitTest
{
    /// <summary>Verifies a case label sharing a section with default is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaseBesideDefaultIsRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int value)
                                  {
                                      switch (value)
                                      {
                                          case 0:
                                              return 1;
                                          {|SST1466:case 1:|}
                                          default:
                                              return 0;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int value)
                                       {
                                           switch (value)
                                           {
                                               case 0:
                                                   return 1;
                                               default:
                                                   return 0;
                                           }
                                       }
                                   }
                                   """;
        await VerifyRemoveCaseBesideDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies every case label sharing a section with default is reported and Fix All removes them all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleCasesBesideDefaultAreAllRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int value)
                                  {
                                      switch (value)
                                      {
                                          case 0:
                                              return 1;
                                          {|SST1466:case 1:|}
                                          {|SST1466:case 2:|}
                                          default:
                                              return 0;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int value)
                                       {
                                           switch (value)
                                           {
                                               case 0:
                                                   return 1;
                                               default:
                                                   return 0;
                                           }
                                       }
                                   }
                                   """;
        await VerifyRemoveCaseBesideDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a pattern label sharing a section with default is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PatternLabelBesideDefaultIsRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int value)
                                  {
                                      switch (value)
                                      {
                                          case 0:
                                              return 1;
                                          {|SST1466:case > 5:|}
                                          default:
                                              return 0;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int value)
                                       {
                                           switch (value)
                                           {
                                               case 0:
                                                   return 1;
                                               default:
                                                   return 0;
                                           }
                                       }
                                   }
                                   """;
        await VerifyRemoveCaseBesideDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a default label alone in its own section is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultAloneIsCleanAsync()
        => await VerifyRemoveCaseBesideDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 0:
                            return 1;
                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies case labels sharing a section without default are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaseLabelsInOtherSectionsAreCleanAsync()
        => await VerifyRemoveCaseBesideDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 0:
                        case 1:
                            return 1;
                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a switch containing a 'goto case' statement is skipped entirely.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GotoCaseSkipsSwitchAsync()
        => await VerifyRemoveCaseBesideDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 0:
                            goto case 1;
                        case 1:
                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a switch containing a 'goto default' statement is skipped entirely.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GotoDefaultSkipsSwitchAsync()
        => await VerifyRemoveCaseBesideDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 0:
                            goto default;
                        case 1:
                        default:
                            return 0;
                    }
                }
            }
            """);
}
