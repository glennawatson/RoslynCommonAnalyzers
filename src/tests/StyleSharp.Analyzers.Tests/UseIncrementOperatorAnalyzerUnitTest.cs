// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyUseIncrementOperator = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2284UseIncrementOperatorAnalyzer,
    StyleSharp.Analyzers.Sst2284UseIncrementOperatorCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2284UseIncrementOperatorAnalyzer"/> and its code fix (SST2284).</summary>
public class UseIncrementOperatorAnalyzerUnitTest
{
    /// <summary>Verifies adding one to a local is reported and rewritten to the increment operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddingOneIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int M(int i)
                                  {
                                      {|SST2284:i += 1|};
                                      return i;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int M(int i)
                                       {
                                           i++;
                                           return i;
                                       }
                                   }
                                   """;
        await VerifyUseIncrementOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies subtracting one is reported and rewritten to the decrement operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubtractingOneIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int _count;

                                  public void M()
                                  {
                                      {|SST2284:this._count -= 1|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int _count;

                                       public void M()
                                       {
                                           this._count--;
                                       }
                                   }
                                   """;
        await VerifyUseIncrementOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nullable value type steps through the lifted operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableValueIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int? i)
                                  {
                                      {|SST2284:i += 1|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int? i)
                                       {
                                           i++;
                                       }
                                   }
                                   """;
        await VerifyUseIncrementOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an enum steps through the operator its underlying type supplies.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumValueIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal enum Level
                              {
                                  Low = 0,
                                  High = 1,
                              }

                              internal class C
                              {
                                  public void M(Level level)
                                  {
                                      {|SST2284:level += 1|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal enum Level
                                   {
                                       Low = 0,
                                       High = 1,
                                   }

                                   internal class C
                                   {
                                       public void M(Level level)
                                       {
                                           level++;
                                       }
                                   }
                                   """;
        await VerifyUseIncrementOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a type that declares its own increment operator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedIncrementIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal struct Counter
                              {
                                  public static Counter operator +(Counter value, int step) => value;

                                  public static Counter operator ++(Counter value) => value;
                              }

                              internal class C
                              {
                                  public void M(Counter counter)
                                  {
                                      {|SST2284:counter += 1|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal struct Counter
                                   {
                                       public static Counter operator +(Counter value, int step) => value;

                                       public static Counter operator ++(Counter value) => value;
                                   }

                                   internal class C
                                   {
                                       public void M(Counter counter)
                                       {
                                           counter++;
                                       }
                                   }
                                   """;
        await VerifyUseIncrementOperator.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a pointer walked forward by one is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PointerStepIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public unsafe int M(int* p)
                                  {
                                      {|SST2284:p += 1|};
                                      return *p;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public unsafe int M(int* p)
                                       {
                                           p++;
                                           return *p;
                                       }
                                   }
                                   """;
        await RunUnsafeAsync(Source, FixedSource);
    }

    /// <summary>Verifies a type that overloads addition but not incrementing keeps the compound form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeWithoutIncrementOperatorIsCleanAsync()
        => await VerifyUseIncrementOperator.VerifyAnalyzerAsync(
            """
            internal struct Money
            {
                public static Money operator +(Money value, int step) => value;
            }

            internal class C
            {
                public void M(Money money)
                {
                    money += 1;
                }
            }
            """);

    /// <summary>Verifies a dynamic target is left alone, because the operator is not known until run time.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DynamicTargetIsCleanAsync()
        => await VerifyUseIncrementOperator.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(dynamic value)
                {
                    value += 1;
                }
            }
            """);

    /// <summary>Verifies an assignment whose value is consumed is left alone; the two forms differ there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsumedAssignmentValueIsCleanAsync()
        => await VerifyUseIncrementOperator.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int i)
                {
                    return i += 1;
                }
            }
            """);

    /// <summary>Verifies a step of something other than one is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StepOtherThanOneIsCleanAsync()
        => await VerifyUseIncrementOperator.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int i)
                {
                    i += 2;
                }
            }
            """);

    /// <summary>Verifies an indexed target is left alone; the rule only rewrites plain names.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexedTargetIsCleanAsync()
        => await VerifyUseIncrementOperator.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int[] values)
                {
                    values[0] += 1;
                }
            }
            """);

    /// <summary>Verifies a string built by appending is left alone; concatenation is not a step.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringAppendIsCleanAsync()
        => await VerifyUseIncrementOperator.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(string text)
                {
                    text += 1;
                }
            }
            """);

    /// <summary>Runs the code fix verifier with unsafe compilation enabled.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunUnsafeAsync(string source, string fixedSource)
    {
        var test = new VerifyUseIncrementOperator.Test
        {
            TestCode = source,
            FixedCode = fixedSource
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
