// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySwitchBraces = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1525SwitchSectionBracesAnalyzer,
    StyleSharp.Analyzers.Sst1525SwitchSectionBracesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the switch-section braces rule (SST1525).</summary>
public class SwitchSectionBracesUnitTest
{
    /// <summary>Verifies a switch section of several bare statements is reported and wrapped in braces.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiStatementSectionWrappedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void M(int x)
                                  {
                                      switch (x)
                                      {
                                          {|SST1525:case 1:|}
                                              x++;
                                              x--;
                                              break;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private void M(int x)
                                       {
                                           switch (x)
                                           {
                                               case 1:
                                               {
                                                   x++;
                                                   x--;
                                                   break;
                                               }
                                           }
                                       }
                                   }
                                   """;
        await VerifySwitchBraces.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a single-statement section and a single-block section are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleStatementAndBlockSectionsAreCleanAsync()
        => await VerifySwitchBraces.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            break;
                        case 2:
                        {
                            x++;
                            break;
                        }
                    }
                }
            }
            """);
}
