// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySwitchExpression = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2201PreferSwitchExpressionAnalyzer,
    StyleSharp.Analyzers.Sst2201PreferSwitchExpressionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2201 (prefer switch expression).</summary>
public class PreferSwitchExpressionAnalyzerUnitTest
{
    /// <summary>Verifies a return-only switch with a default section is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnOnlySwitchIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int value)
                                  {
                                      {|SST2201:switch|} (value)
                                      {
                                          case 0:
                                              return 1;
                                          case 1:
                                              return 2;
                                          default:
                                              return 3;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int value)
                                       {
                                           return value switch
                                           {
                                               0 => 1,
                                               1 => 2,
                                               _ => 3
                                           };
                                       }
                                   }
                                   """;
        await VerifySwitchExpression.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies throw arms are accepted as switch-expression arms.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowArmSwitchIsFixedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M(int value)
                                  {
                                      {|SST2201:switch|} (value)
                                      {
                                          case 0:
                                              return 1;
                                          default:
                                              throw new InvalidOperationException();
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public int M(int value)
                                       {
                                           return value switch
                                           {
                                               0 => 1,
                                               _ => throw new InvalidOperationException()
                                           };
                                       }
                                   }
                                   """;
        await VerifySwitchExpression.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies switches that need statement bodies or lack a default are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonExpressionSwitchesAreCleanAsync()
        => await VerifySwitchExpression.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int MissingDefault(int value)
                {
                    switch (value)
                    {
                        case 0:
                            return 1;
                    }

                    return 2;
                }

                public int HasExtraStatement(int value)
                {
                    switch (value)
                    {
                        case 0:
                            value++;
                            return value;
                        default:
                            return 2;
                    }
                }

                public int HasStackedLabels(int value)
                {
                    switch (value)
                    {
                        case 0:
                        case 1:
                            return 1;
                        default:
                            return 2;
                    }
                }
            }
            """);
}
