// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeConcatenation = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1206StringConcatenationInLoopAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1206 (string concatenation inside a loop is quadratic).</summary>
public class StringConcatenationInLoopAnalyzerUnitTest
{
    /// <summary>Verifies a += on a local declared before a for loop is reported (PSH1206).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddAssignmentInForLoopReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(int count)
                                  {
                                      var result = string.Empty;
                                      for (var i = 0; i < count; i++)
                                      {
                                          result {|PSH1206:+=|} i;
                                      }

                                      return result;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source);
    }

    /// <summary>Verifies a self-concatenating simple assignment in a while loop is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfConcatenationInWhileLoopReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(int count)
                                  {
                                      var s = string.Empty;
                                      var i = 0;
                                      while (i < count)
                                      {
                                          s {|PSH1206:=|} s + i;
                                          i++;
                                      }

                                      return s;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source);
    }

    /// <summary>Verifies a += on a local declared inside the loop is not reported — it starts fresh each iteration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalDeclaredInsideLoopIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(int count)
                                  {
                                      for (var i = 0; i < count; i++)
                                      {
                                          var line = string.Empty;
                                          line += i;
                                          System.Console.WriteLine(line);
                                      }
                                  }
                              }
                              """;
        await VerifyNet90Async(Source);
    }

    /// <summary>Verifies a += outside any loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddAssignmentOutsideLoopIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string value)
                                  {
                                      var s = string.Empty;
                                      s += value;
                                      return s;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source);
    }

    /// <summary>Verifies an int accumulator in a loop is not reported — the rule only tracks strings.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntAccumulatorInLoopIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int count)
                                  {
                                      var total = 0;
                                      for (var i = 0; i < count; i++)
                                      {
                                          total += i;
                                      }

                                      return total;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source);
    }

    /// <summary>Verifies a += on a string field inside a foreach loop is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldAddAssignmentInForeachReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private string _log = string.Empty;

                                  public string M(string[] items)
                                  {
                                      foreach (var item in items)
                                      {
                                          _log {|PSH1206:+=|} item;
                                      }

                                      return _log;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source);
    }

    /// <summary>Verifies a += inside a lambda is not reported when the loop is outside the lambda.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaInsideLoopIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(int count)
                                  {
                                      var s = string.Empty;
                                      for (var i = 0; i < count; i++)
                                      {
                                          System.Action append = () =>
                                          {
                                              s += "x";
                                          };
                                          append();
                                      }

                                      return s;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source);
    }

    /// <summary>Verifies a loop fully inside a lambda is reported when the local is declared in the lambda above the loop.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoopInsideLambdaReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(int count)
                                  {
                                      System.Func<string> build = () =>
                                      {
                                          var s = string.Empty;
                                          for (var i = 0; i < count; i++)
                                          {
                                              s {|PSH1206:+=|} i;
                                          }

                                          return s;
                                      };

                                      return build();
                                  }
                              }
                              """;
        await VerifyNet90Async(Source);
    }

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeConcatenation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
