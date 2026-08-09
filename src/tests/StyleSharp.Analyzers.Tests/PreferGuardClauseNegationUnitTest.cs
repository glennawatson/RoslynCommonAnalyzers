// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyGuardNegation = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2273PreferGuardClauseAnalyzer,
    StyleSharp.Analyzers.Sst2273PreferGuardClauseCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Tests that SST2273's guard clause tests the opposite of the <c>if</c> it replaces, across the condition
/// shapes whose negation is not a bare <c>!</c>. An inverted guard compiles clean and reverses which work
/// runs, so each shape is pinned rather than left to the general case.
/// </summary>
public class PreferGuardClauseNegationUnitTest
{
    /// <summary>Verifies a negated call guarding loop work is inverted, not carried over unchanged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedCallGuardingLoopWorkIsInvertedAsync()
    {
        const string Source = """
                              using System.IO;

                              public sealed class C
                              {
                                  public string[] Split(string[] candidates, int index)
                                  {
                                      var result = new string[candidates.Length];
                                      foreach (var candidate in candidates)
                                      {
                                          var name = Path.GetFileName(candidate);
                                          {|SST2273:if|} (!string.IsNullOrEmpty(name))
                                          {
                                              result[index] = name;
                                              index--;
                                          }
                                      }

                                      return result;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.IO;

                                   public sealed class C
                                   {
                                       public string[] Split(string[] candidates, int index)
                                       {
                                           var result = new string[candidates.Length];
                                           foreach (var candidate in candidates)
                                           {
                                               var name = Path.GetFileName(candidate);
                                               if (string.IsNullOrEmpty(name))
                                               {
                                                   continue;
                                               }

                                               result[index] = name;
                                               index--;
                                           }

                                           return result;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a conjunction distributes by De Morgan rather than being wrapped whole.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConjunctionDistributesByDeMorganAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool ready, int count)
                                  {
                                      {|SST2273:if|} (!ready && count > 0)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool ready, int count)
                                       {
                                           if (ready || count <= 0)
                                           {
                                               return;
                                           }

                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a floating-point comparison is wrapped, not flipped; the two disagree on NaN.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>!(a &lt; b)</c> is true when either operand is NaN, where <c>a &gt;= b</c> is false, so the flip
    /// that is exact for integers would change the answer here.
    /// </remarks>
    [Test]
    public async Task FloatingPointComparisonIsWrappedNotFlippedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(double left, double right)
                                  {
                                      {|SST2273:if|} (left < right)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(double left, double right)
                                       {
                                           if (!(left < right))
                                           {
                                               return;
                                           }

                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nullable comparison is wrapped, not flipped; a null operand makes both sides false.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableComparisonIsWrappedNotFlippedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int? count)
                                  {
                                      {|SST2273:if|} (count > 0)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(int? count)
                                       {
                                           if (!(count > 0))
                                           {
                                               return;
                                           }

                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a disjunction distributes to a conjunction, parenthesizing where precedence needs it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisjunctionDistributesByDeMorganAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool ready, int count, bool other)
                                  {
                                      {|SST2273:if|} (ready || (count > 0 && other))
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool ready, int count, bool other)
                                       {
                                           if (!ready && (count <= 0 || !other))
                                           {
                                               return;
                                           }

                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a not-pattern is negated by dropping the <c>not</c>, not by wrapping the whole test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotPatternIsNegatedByDroppingTheNotAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(string text)
                                  {
                                      {|SST2273:if|} (text is not null)
                                      {
                                          System.Console.WriteLine(text);
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(string text)
                                       {
                                           if (text is null)
                                           {
                                               return;
                                           }

                                           System.Console.WriteLine(text);
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a type pattern is negated by adding a <c>not</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypePatternIsNegatedByAddingTheNotAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(object value)
                                  {
                                      {|SST2273:if|} (value is string)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(object value)
                                       {
                                           if (value is not string)
                                           {
                                               return;
                                           }

                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a declaration pattern, which cannot carry a <c>not</c>, is wrapped instead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclarationPatternIsWrappedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(object value)
                                  {
                                      {|SST2273:if|} (value is string text)
                                      {
                                          System.Console.WriteLine(text);
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(object value)
                                       {
                                           if (!(value is string text))
                                           {
                                               return;
                                           }

                                           System.Console.WriteLine(text);
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource)
    {
        var test = new VerifyGuardNegation.Test
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        const string Config = """
                              root = true

                              [*.cs]
                              dotnet_diagnostic.SST2273.severity = warning

                              """;
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        await test.RunAsync(CancellationToken.None);
    }
}
