// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedundantSwitchSectionBraces = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1534RedundantSwitchSectionBracesAnalyzer,
    StyleSharp.Analyzers.Sst1534RedundantSwitchSectionBracesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1534RedundantSwitchSectionBracesAnalyzer"/> and its code fix (SST1534).</summary>
public class RedundantSwitchSectionBracesAnalyzerUnitTest
{
    /// <summary>Verifies braces around a section that declares nothing are reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracesScopingNothingAreFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int M(int value)
                                  {
                                      switch (value)
                                      {
                                          case 1:
                                              {|SST1534:{|}
                                                  return 2;
                                              }

                                          default:
                                              return 0;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int M(int value)
                                       {
                                           switch (value)
                                           {
                                               case 1:
                                                   return 2;

                                               default:
                                                   return 0;
                                           }
                                       }
                                   }
                                   """;
        await VerifyRedundantSwitchSectionBraces.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies braces scoping a local are kept; a sibling section could declare the same name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracesScopingALocalAreCleanAsync()
        => await VerifyRedundantSwitchSectionBraces.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 1:
                            {
                                var result = 2;
                                return result;
                            }

                        default:
                            {
                                var result = 0;
                                return result;
                            }
                    }
                }
            }
            """);

    /// <summary>Verifies braces scoping a pattern variable are kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracesScopingAPatternVariableAreCleanAsync()
        => await VerifyRedundantSwitchSectionBraces.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(object value)
                {
                    switch (value)
                    {
                        case 1:
                            {
                                if (value is string text)
                                {
                                    return text.Length;
                                }

                                return 1;
                            }

                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies braces scoping a local function are kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracesScopingALocalFunctionAreCleanAsync()
        => await VerifyRedundantSwitchSectionBraces.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 1:
                            {
                                return Double(value);

                                int Double(int v) => v * 2;
                            }

                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a section with no braces is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnbracedSectionIsCleanAsync()
        => await VerifyRedundantSwitchSectionBraces.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 1:
                            return 2;

                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies an empty braced section is left alone; there is nothing to lift.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyBracedSectionIsCleanAsync()
        => await VerifyRedundantSwitchSectionBraces.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        default:
                            {
                            }

                            return 0;
                    }
                }
            }
            """);
}
