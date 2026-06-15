// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAccessorOrder = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.AccessorOrderAnalyzer,
    StyleSharp.Analyzers.AccessorOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the accessor-order rules (SST1212/SST1213).</summary>
public class OrderingAccessorUnitTest
{
    /// <summary>Verifies a get accessor after a set accessor is reported (SST1212) and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetAfterSetReorderedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int _x;

                                  public int X
                                  {
                                      set => _x = value;
                                      {|SST1212:get|} => _x;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int _x;

                                       public int X
                                       {
                                           get => _x;
                                           set => _x = value;
                                       }
                                   }
                                   """;
        await VerifyAccessorOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a get-before-set property is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetBeforeSetIsCleanAsync()
        => await VerifyAccessorOrder.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int _x;

                public int X
                {
                    get => _x;
                    set => _x = value;
                }
            }
            """);

    /// <summary>Verifies Fix All reorders every misordered accessor pair in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int _x;
                                  private int _y;

                                  public int X
                                  {
                                      set => _x = value;
                                      {|SST1212:get|} => _x;
                                  }

                                  public int Y
                                  {
                                      set => _y = value;
                                      {|SST1212:get|} => _y;
                                  }

                                  public event System.EventHandler E
                                  {
                                      remove { }
                                      {|SST1213:add|} { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int _x;
                                       private int _y;

                                       public int X
                                       {
                                           get => _x;
                                           set => _x = value;
                                       }

                                       public int Y
                                       {
                                           get => _y;
                                           set => _y = value;
                                       }

                                       public event System.EventHandler E
                                       {
                                           add { }
                                           remove { }
                                       }
                                   }
                                   """;
        await VerifyAccessorOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an add accessor after a remove accessor is reported (SST1213) and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddAfterRemoveReorderedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public event System.EventHandler E
                                  {
                                      remove { }
                                      {|SST1213:add|} { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public event System.EventHandler E
                                       {
                                           add { }
                                           remove { }
                                       }
                                   }
                                   """;
        await VerifyAccessorOrder.VerifyCodeFixAsync(Source, FixedSource);
    }
}
