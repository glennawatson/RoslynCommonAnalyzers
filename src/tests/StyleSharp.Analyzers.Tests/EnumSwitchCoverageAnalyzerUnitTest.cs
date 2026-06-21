// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyEnumSwitchCoverage = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.EnumSwitchCoverageAnalyzer,
    StyleSharp.Analyzers.EnumSwitchCoverageCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for enum switch coverage rules (SST2205/SST2206).</summary>
public class EnumSwitchCoverageAnalyzerUnitTest
{
    /// <summary>Verifies a switch statement missing an enum case is fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingSwitchStatementCaseIsFixedAsync()
    {
        const string Source = """
                              public enum Color
                              {
                                  Red,
                                  Blue
                              }

                              public sealed class C
                              {
                                  public void M(Color color)
                                  {
                                      {|SST2205:switch|} (color)
                                      {
                                          case Color.Red:
                                              break;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public enum Color
                                   {
                                       Red,
                                       Blue
                                   }

                                   public sealed class C
                                   {
                                       public void M(Color color)
                                       {
                                           switch (color)
                                           {
                                               case Color.Red:
                                                   break;
                                               case global::Color.Blue:
                                                   break;
                                           }
                                       }
                                   }
                                   """;
        var test = new VerifyEnumSwitchCoverage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a switch expression missing an enum arm is fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingSwitchExpressionArmIsFixedAsync()
    {
        const string Source = """
                              public enum Color
                              {
                                  Red,
                                  Blue
                              }

                              public sealed class C
                              {
                                  public int M(Color color) => color {|SST2206:switch|}
                                  {
                                      Color.Red => 1
                                  };
                              }
                              """;
        const string FixedSource = """
                                   public enum Color
                                   {
                                       Red,
                                       Blue
                                   }

                                   public sealed class C
                                   {
                                       public int M(Color color) => color switch
                                       {
                                           Color.Red => 1,
                                           global::Color.Blue => throw new global::System.NotImplementedException()
                                       };
                                   }
                                   """;
        var test = new VerifyEnumSwitchCoverage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies switches with a catch-all case are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CatchAllSwitchesAreCleanAsync()
    {
        const string Source = """
                              public enum Color
                              {
                                  Red,
                                  Blue
                              }

                              public sealed class C
                              {
                                  public void Statement(Color color)
                                  {
                                      switch (color)
                                      {
                                          case Color.Red:
                                              break;
                                          default:
                                              break;
                                      }
                                  }

                                  public int Expression(Color color) => color switch
                                  {
                                      Color.Red => 1,
                                      _ => 2
                                  };
                              }
                              """;
        var test = new VerifyEnumSwitchCoverage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
