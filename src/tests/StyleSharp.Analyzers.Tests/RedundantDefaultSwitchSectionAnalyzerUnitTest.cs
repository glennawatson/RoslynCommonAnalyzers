// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDefaultSection = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantCodeAnalyzer,
    StyleSharp.Analyzers.RedundantDefaultSwitchSectionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1179 (redundant default switch section) and its fix.</summary>
public class RedundantDefaultSwitchSectionAnalyzerUnitTest
{
    /// <summary>Verifies a <c>default:</c> section that only breaks is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultThatOnlyBreaksRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(int value)
                                  {
                                      switch (value)
                                      {
                                          case 1:
                                              return;
                                          {|SST1179:default|}:
                                              break;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(int value)
                                       {
                                           switch (value)
                                           {
                                               case 1:
                                                   return;
                                           }
                                       }
                                   }
                                   """;
        await VerifyDefaultSection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a default section that does real work is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultWithWorkIsCleanAsync()
        => await VerifyDefaultSection.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 1:
                            return 1;
                        default:
                            return 0;
                    }
                }
            }
            """);
}
