// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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

    /// <summary>Verifies Fix All removes every redundant default section across a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void A(int value)
                                  {
                                      switch (value)
                                      {
                                          case 1:
                                              return;
                                          {|SST1179:default|}:
                                              break;
                                      }
                                  }

                                  public void B(int value)
                                  {
                                      switch (value)
                                      {
                                          case 2:
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
                                       public void A(int value)
                                       {
                                           switch (value)
                                           {
                                               case 1:
                                                   return;
                                           }
                                       }

                                       public void B(int value)
                                       {
                                           switch (value)
                                           {
                                               case 2:
                                                   return;
                                           }
                                       }
                                   }
                                   """;
        await VerifyDefaultSection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a region around the removed section is left behind rather than half-deleted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The <c>#region</c> is the section's leading trivia and the <c>#endregion</c> is not, so taking the
    /// section's trivia with it would leave a close with nothing to close — CS1028.
    /// </remarks>
    [Test]
    public async Task RegionAroundTheRemovedSectionSurvivesAsync()
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
                              #region Fallback
                                          {|SST1179:default|}:
                                              break;
                              #endregion
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

                                   #region Fallback
                                   #endregion
                                           }
                                       }
                                   }
                                   """;
        await VerifyDefaultSection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a breaking default section over an enum is left in place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The section is what marks the mapping deliberately partial, so removing it turns the switch into
    /// an incomplete enum mapping (SST2242).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BreakingDefaultOverAnEnumIsCleanAsync() =>
        VerifyDefaultSection.VerifyAnalyzerAsync(
            """
            public enum Level
            {
                Low,
                High
            }

            public class C
            {
                public void M(Level level)
                {
                    switch (level)
                    {
                        case Level.Low:
                            return;
                        default:
                            break;
                    }
                }
            }
            """);

    /// <summary>Verifies a default section that does real work is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DefaultWithWorkIsCleanAsync() =>
        VerifyDefaultSection.VerifyAnalyzerAsync(
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
