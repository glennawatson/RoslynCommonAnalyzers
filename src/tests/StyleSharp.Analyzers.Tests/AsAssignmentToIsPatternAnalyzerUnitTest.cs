// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAsPattern = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2274AsAssignmentToIsPatternAnalyzer,
    StyleSharp.Analyzers.Sst2274AsAssignmentToIsPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2274 (convert an <c>as</c> assignment plus null check into an <c>is</c> declaration
/// pattern) and its code fix.
/// </summary>
public class AsAssignmentToIsPatternAnalyzerUnitTest
{
    /// <summary>Verifies the guarded-use shape becomes a positive declaration pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedUseBecomesDeclarationPatternAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object o)
                                  {
                                      var {|SST2274:s|} = o as string;
                                      if (s != null)
                                      {
                                          return s.Length;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(object o)
                                       {
                                           if (o is string s)
                                           {
                                               return s.Length;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyAsPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an <c>is not null</c> condition also becomes a positive declaration pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNotNullConditionBecomesDeclarationPatternAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object o)
                                  {
                                      var {|SST2274:s|} = o as string;
                                      if (s is not null)
                                      {
                                          return s.Length;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(object o)
                                       {
                                           if (o is string s)
                                           {
                                               return s.Length;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyAsPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the null early-exit shape becomes a negated declaration pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullEarlyExitBecomesNegatedPatternAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object o)
                                  {
                                      var {|SST2274:s|} = o as string;
                                      if (s == null)
                                      {
                                          return 0;
                                      }

                                      return s.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(object o)
                                       {
                                           if (o is not string s)
                                           {
                                               return 0;
                                           }

                                           return s.Length;
                                       }
                                   }
                                   """;
        await VerifyAsPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an <c>is null</c> guard that throws becomes a negated declaration pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullThrowGuardBecomesNegatedPatternAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object o)
                                  {
                                      var {|SST2274:s|} = o as string;
                                      if (s is null)
                                      {
                                          throw new System.InvalidOperationException();
                                      }

                                      return s.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(object o)
                                       {
                                           if (o is not string s)
                                           {
                                               throw new System.InvalidOperationException();
                                           }

                                           return s.Length;
                                       }
                                   }
                                   """;
        await VerifyAsPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a loop <c>continue</c> guard becomes a negated declaration pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoopContinueGuardBecomesNegatedPatternAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public void M(IEnumerable<object> items)
                                  {
                                      foreach (var o in items)
                                      {
                                          var {|SST2274:s|} = o as string;
                                          if (s == null)
                                          {
                                              continue;
                                          }

                                          System.Console.WriteLine(s.Length);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public void M(IEnumerable<object> items)
                                       {
                                           foreach (var o in items)
                                           {
                                               if (o is not string s)
                                               {
                                                   continue;
                                               }

                                               System.Console.WriteLine(s.Length);
                                           }
                                       }
                                   }
                                   """;
        await VerifyAsPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an explicitly nullable-annotated local type is folded into the pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableAnnotatedLocalIsFixedAsync()
    {
        const string Source = """
                              #nullable enable
                              public sealed class C
                              {
                                  public int M(object? o)
                                  {
                                      string? {|SST2274:s|} = o as string;
                                      if (s != null)
                                      {
                                          return s.Length;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public sealed class C
                                   {
                                       public int M(object? o)
                                       {
                                           if (o is string s)
                                           {
                                               return s.Length;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyAsPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies two occurrences in one document are each reported and fixed, exercising Fix All.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int First(object o)
                                  {
                                      var {|SST2274:s|} = o as string;
                                      if (s != null)
                                      {
                                          return s.Length;
                                      }

                                      return 0;
                                  }

                                  public int Second(object o)
                                  {
                                      var {|SST2274:t|} = o as string;
                                      if (t == null)
                                      {
                                          return 0;
                                      }

                                      return t.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int First(object o)
                                       {
                                           if (o is string s)
                                           {
                                               return s.Length;
                                           }

                                           return 0;
                                       }

                                       public int Second(object o)
                                       {
                                           if (o is not string t)
                                           {
                                               return 0;
                                           }

                                           return t.Length;
                                       }
                                   }
                                   """;
        await VerifyAsPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an <c>as</c> to a nullable value type is left alone (the pattern would change the type).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableValueTypeTargetIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    var s = o as int?;
                    if (s != null)
                    {
                        return s.Value;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a local reassigned inside the guarded branch is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReassignedLocalIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    var s = o as string;
                    if (s != null)
                    {
                        s = string.Empty;
                        return s.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a null check that is not the immediately following statement is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAdjacentNullCheckIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    var s = o as string;
                    var count = 0;
                    if (s != null)
                    {
                        return s.Length + count;
                    }

                    return count;
                }
            }
            """);

    /// <summary>Verifies an immediately following <c>if</c> that does not null-check the local is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedConditionIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o, bool ready)
                {
                    var s = o as string;
                    if (ready)
                    {
                        return s == null ? 0 : s.Length;
                    }

                    return -1;
                }
            }
            """);

    /// <summary>Verifies a null pattern that tests a different value than the local is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullPatternOnOtherValueIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o, object other)
                {
                    var s = o as string;
                    if (other is null)
                    {
                        return 0;
                    }

                    return s == null ? 1 : s.Length;
                }
            }
            """);

    /// <summary>Verifies passing the local by reference in the guarded branch leaves it alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByReferenceArgumentIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private static void Consume(ref string value) => value = value ?? string.Empty;

                public int M(object o)
                {
                    var s = o as string;
                    if (s != null)
                    {
                        Consume(ref s);
                        return s.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies aliasing the local with a <c>ref</c> local in the guarded branch leaves it alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RefLocalAliasIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    var s = o as string;
                    if (s != null)
                    {
                        ref var alias = ref s;
                        alias = string.Empty;
                        return s.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies the guarded-use shape is left alone when the local is read after the if.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedUseWithReadAfterIfIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    var s = o as string;
                    if (s != null)
                    {
                        System.Console.WriteLine(s.Length);
                    }

                    return s == null ? 0 : 1;
                }
            }
            """);

    /// <summary>Verifies a side-effecting <c>as</c> operand is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SideEffectingOperandIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private object Get() => "";

                public int M()
                {
                    var s = Get() as string;
                    if (s != null)
                    {
                        return s.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a multi-declarator declaration is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleDeclaratorsIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    object s = o as string, other = null;
                    if (s != null)
                    {
                        System.Console.WriteLine(other);
                        return 1;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies the early-exit shape is left alone when the guard body reads the local.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EarlyExitGuardReadingLocalIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    var s = o as string;
                    if (s == null)
                    {
                        System.Console.WriteLine(s);
                        return 0;
                    }

                    return s.Length;
                }
            }
            """);

    /// <summary>Verifies a null guard whose body can fall through is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonExitingNullGuardIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    var s = o as string;
                    if (s == null)
                    {
                        System.Console.WriteLine("null");
                    }

                    return s == null ? 0 : s.Length;
                }
            }
            """);

    /// <summary>Verifies a compound null-check condition is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundConditionIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    var s = o as string;
                    if (s != null && s.Length > 0)
                    {
                        return s.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a local declared as a base type of the <c>as</c> target is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseTypedLocalIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o)
                {
                    object s = o as string;
                    if (s != null)
                    {
                        return s.GetHashCode();
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a braceless early-exit guard is folded into a negated pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracelessNullGuardBecomesNegatedPatternAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object o)
                                  {
                                      var {|SST2274:s|} = o as string;
                                      if (s == null)
                                          return 0;

                                      return s.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(object o)
                                       {
                                           if (o is not string s)
                                               return 0;

                                           return s.Length;
                                       }
                                   }
                                   """;
        await VerifyAsPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>using</c> declaration is left alone so its disposal is not dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingDeclarationIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(object o)
                {
                    using var s = o as IDisposable;
                    if (s != null)
                    {
                        System.Console.WriteLine(s);
                    }
                }
            }
            """);

    /// <summary>Verifies a local declared directly in a switch section (not a block) is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchSectionLocalIsCleanAsync()
        => await VerifyAsPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object o, int n)
                {
                    switch (n)
                    {
                        case 1:
                            var s = o as string;
                            if (s != null)
                            {
                                return s.Length;
                            }

                            return 0;
                        default:
                            return -1;
                    }
                }
            }
            """);
}
