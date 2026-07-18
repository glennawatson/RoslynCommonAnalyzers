// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using VerifyPreferConst = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1402PreferConstOverStaticReadonlyAnalyzer,
    PerformanceSharp.Analyzers.Psh1402PreferConstOverStaticReadonlyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1402 (prefer const over static readonly) and its fix.</summary>
public class PreferConstOverStaticReadonlyAnalyzerUnitTest
{
    /// <summary>Verifies a private static readonly int with a literal value becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateStaticReadonlyIntBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static readonly int {|PSH1402:MaxRetries|} = 3;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private const int MaxRetries = 3;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an internal static readonly string becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalStaticReadonlyStringBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  internal static readonly string {|PSH1402:Prefix|} = "app";
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       internal const string Prefix = "app";
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a field initialized from another const becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstReferenceInitializerBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private const int Base = 10;
                                  private static readonly int {|PSH1402:Limit|} = Base * 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private const int Base = 10;
                                       private const int Limit = Base * 2;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an enum-typed field with a constant member initializer becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumTypedConstantBecomesConstAsync()
    {
        const string Source = """
                              public enum Level
                              {
                                  None = 0,
                                  High = 2,
                              }

                              public class C
                              {
                                  private static readonly Level {|PSH1402:DefaultLevel|} = Level.High;
                              }
                              """;
        const string FixedSource = """
                                   public enum Level
                                   {
                                       None = 0,
                                       High = 2,
                                   }

                                   public class C
                                   {
                                       private const Level DefaultLevel = Level.High;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies remaining modifiers such as new survive the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewModifierIsPreservedAsync()
    {
        const string Source = """
                              public class B
                              {
                                  internal const int Value = 1;
                              }

                              public class D : B
                              {
                                  private new static readonly int {|PSH1402:Value|} = 2;
                              }
                              """;
        const string FixedSource = """
                                   public class B
                                   {
                                       internal const int Value = 1;
                                   }

                                   public class D : B
                                   {
                                       private new const int Value = 2;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All converts every reported field in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static readonly int {|PSH1402:First|} = 1;
                                  private static readonly int {|PSH1402:Second|} = 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private const int First = 1;
                                       private const int Second = 2;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a public static readonly field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicStaticReadonlyIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public static readonly int MaxRetries = 3;
            }
            """);

    /// <summary>Verifies a non-constant initializer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewObjectInitializerIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly object Gate = new object();
            }
            """);

    /// <summary>Verifies an instance readonly field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStaticReadonlyIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _count = 3;
            }
            """);

    /// <summary>Verifies a static readonly field of a type that cannot be const is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReadonlyGuidIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly System.Guid Id = new System.Guid("7f8b52e8-84a9-4f43-a58a-1f0ee9b56b4d");
            }
            """);

    /// <summary>Verifies a multi-variable declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiVariableDeclarationIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly int A = 1, B = 2;
            }
            """);

    /// <summary>Verifies a never-reassigned local with a literal initializer becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalWithLiteralBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Compute()
                                  {
                                      int {|PSH1402:max|} = 3;
                                      int result;
                                      result = max;
                                      return result;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Compute()
                                       {
                                           const int max = 3;
                                           int result;
                                           result = max;
                                           return result;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a var local becomes const with the inferred type spelled out.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VarLocalBecomesConstWithExplicitTypeAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string Name()
                                  {
                                      var {|PSH1402:prefix|} = "app";
                                      return prefix;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string Name()
                                       {
                                           const string prefix = "app";
                                           return prefix;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local initialized from constant arithmetic becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFromConstExpressionBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private const int Base = 10;

                                  public int Compute()
                                  {
                                      int {|PSH1402:limit|} = Base * 2;
                                      return limit;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private const int Base = 10;

                                       public int Compute()
                                       {
                                           const int limit = Base * 2;
                                           return limit;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an enum-typed var local becomes const with the enum type spelled out.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumTypedVarLocalBecomesConstAsync()
    {
        const string Source = """
                              public enum Level
                              {
                                  None = 0,
                                  High = 2,
                              }

                              public class C
                              {
                                  public Level Pick()
                                  {
                                      var {|PSH1402:level|} = Level.High;
                                      return level;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public enum Level
                                   {
                                       None = 0,
                                       High = 2,
                                   }

                                   public class C
                                   {
                                       public Level Pick()
                                       {
                                           const Level level = Level.High;
                                           return level;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a string local initialized to null becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullInitializedStringLocalBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string Missing()
                                  {
                                      string {|PSH1402:missing|} = null;
                                      return missing;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string Missing()
                                       {
                                           const string missing = null;
                                           return missing;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local initialized with nameof becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameofInitializedLocalBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string Name()
                                  {
                                      var {|PSH1402:name|} = nameof(C);
                                      return name;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string Name()
                                       {
                                           const string name = nameof(C);
                                           return name;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local only read inside a lambda still becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalReadInsideLambdaBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<int> Make()
                                  {
                                      int {|PSH1402:seed|} = 3;
                                      return () => seed;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.Func<int> Make()
                                       {
                                           const int seed = 3;
                                           return () => seed;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local read through a tuple expression still becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TupleReadLocalBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public (int, int) Pair()
                                  {
                                      int {|PSH1402:half|} = 3;
                                      return (half, half);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public (int, int) Pair()
                                       {
                                           const int half = 3;
                                           return (half, half);
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local declared directly in a switch section becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchSectionLocalBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Pick(int input)
                                  {
                                      switch (input)
                                      {
                                          case 1:
                                              int {|PSH1402:bonus|} = 3;
                                              return input + bonus;
                                          default:
                                              return input;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Pick(int input)
                                       {
                                           switch (input)
                                           {
                                               case 1:
                                                   const int bonus = 3;
                                                   return input + bonus;
                                               default:
                                                   return input;
                                           }
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies assigning a same-named field through this does not block the local.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedFieldAssignmentDoesNotBlockLocalConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int total;

                                  public int Snapshot()
                                  {
                                      int {|PSH1402:total|} = 3;
                                      this.total = total;
                                      return this.total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int total;

                                       public int Snapshot()
                                       {
                                           const int total = 3;
                                           this.total = total;
                                           return this.total;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a top-level-statement local becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TopLevelStatementLocalBecomesConstAsync()
    {
        var test = new VerifyPreferConst.Test
        {
            TestCode = """
                       int {|PSH1402:max|} = 3;
                       System.Console.WriteLine(max);
                       """,
            FixedCode = """
                        const int max = 3;
                        System.Console.WriteLine(max);
                        """,
        };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies Fix All converts every reported local in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryLocalAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Compute()
                                  {
                                      int {|PSH1402:first|} = 1;
                                      int {|PSH1402:second|} = 2;
                                      return first + second;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Compute()
                                       {
                                           const int first = 1;
                                           const int second = 2;
                                           return first + second;
                                       }
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a reassigned local is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReassignedLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int total = 3;
                    total = 4;
                    return total;
                }
            }
            """);

    /// <summary>Verifies a compound-assigned local is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundAssignedLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int total = 3;
                    total += 2;
                    return total;
                }
            }
            """);

    /// <summary>Verifies incremented and decremented locals are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IncrementedAndDecrementedLocalsAreCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int postIncremented = 0;
                    int postDecremented = 0;
                    int preIncremented = 0;
                    int preDecremented = 0;
                    postIncremented++;
                    postDecremented--;
                    ++preIncremented;
                    --preDecremented;
                    return postIncremented + postDecremented + preIncremented + preDecremented;
                }
            }
            """);

    /// <summary>Verifies a local passed by ref is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RefArgumentLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int seed = 3;
                    Mutate(ref seed);
                    return seed;
                }

                private static void Mutate(ref int value) => value++;
            }
            """);

    /// <summary>Verifies a local passed by in is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InArgumentLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int seed = 3;
                    return Read(in seed);
                }

                private static int Read(in int value) => value;
            }
            """);

    /// <summary>Verifies a local passed by out is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutArgumentLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int parsed = 0;
                    int.TryParse("3", out parsed);
                    return parsed;
                }
            }
            """);

    /// <summary>Verifies a local aliased by a ref local is not reported, and neither is the alias.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RefLocalAliasIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int value = 3;
                    ref int alias = ref value;
                    alias = 5;
                    return value;
                }
            }
            """);

    /// <summary>Verifies a local whose address is taken is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddressOfLocalIsCleanAsync()
    {
        var test = new VerifyPreferConst.Test
        {
            TestCode = """
                       public class C
                       {
                           public unsafe int Read()
                           {
                               int value = 3;
                               int* pointer = &value;
                               return *pointer;
                           }
                       }
                       """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a local referenced by __makeref is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MakeRefLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Read()
                {
                    int value = 3;
                    System.TypedReference reference = __makeref(value);
                }
            }
            """);

    /// <summary>Verifies a local mutated inside a lambda is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutatedCaptureLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public System.Action Make()
                {
                    int seed = 3;
                    return () => seed = 5;
                }
            }
            """);

    /// <summary>Verifies a local written by deconstruction is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeconstructionAssignedLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int written = 1;
                    (written, var extra) = (2, 3);
                    return written + extra;
                }
            }
            """);

    /// <summary>Verifies a local written by nested deconstruction is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedDeconstructionAssignedLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int inner = 1;
                    int other = 2;
                    int third = 3;
                    ((inner, other), third) = ((4, 5), 6);
                    return inner + other + third;
                }
            }
            """);

    /// <summary>Verifies locals with never-constant initializer shapes are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantInitializerShapesAreCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Read(int[] sizes)
                {
                    var gate = new object();
                    object fallback = new();
                    int[] created = new int[3];
                    var inferred = new[] { 3 };
                    int[] expression = [3];
                    int first = sizes[0];
                    int computed = Compute();
                    return created.Length + inferred.Length + expression.Length + first + computed + (gate == fallback ? 1 : 0);
                }

                private static int Compute() => 3;
            }
            """);

    /// <summary>Verifies a local initialized from an await expression is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitInitializedLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public async System.Threading.Tasks.Task<int> ReadAsync()
                {
                    var value = await System.Threading.Tasks.Task.FromResult(3);
                    return value;
                }
            }
            """);

    /// <summary>Verifies a local of a type that cannot be const is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstCapableTypedLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public System.Guid Read()
                {
                    System.Guid id = default;
                    return id;
                }
            }
            """);

    /// <summary>Verifies a multi-variable local declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiVariableLocalDeclarationIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int a = 1, b = 2;
                    return a + b;
                }
            }
            """);

    /// <summary>Verifies an already-const local is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    const int max = 3;
                    return max;
                }
            }
            """);

    /// <summary>Verifies a using declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingDeclarationLocalIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Read()
                {
                    using var stream = new System.IO.MemoryStream();
                    stream.Flush();
                }
            }
            """);

    /// <summary>Verifies a for-loop variable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForLoopVariableIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute()
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        total += i;
                    }

                    return total;
                }
            }
            """);
}
