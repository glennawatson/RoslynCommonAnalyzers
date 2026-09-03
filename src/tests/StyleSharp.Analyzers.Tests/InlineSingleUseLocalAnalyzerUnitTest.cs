// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInlineSingleUseLocal = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2266InlineSingleUseLocalAnalyzer,
    StyleSharp.Analyzers.Sst2266InlineSingleUseLocalCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2266 (inline a single-use local). The rule is disabled by default, so every test enables
/// it through an <c>.editorconfig</c> severity entry.
/// </summary>
public class InlineSingleUseLocalAnalyzerUnitTest
{
    /// <summary>A declaration whose initializer is wider than the default threshold.</summary>
    private const string LongInitializerSource = """
                                                 public sealed class C
                                                 {
                                                     public bool M()
                                                     {
                                                         var empty = default(System.Collections.Generic.List<int>);
                                                         return empty is null;
                                                     }
                                                 }
                                                 """;

    /// <summary>The same declaration, marked up for a run whose threshold admits it.</summary>
    private const string LongInitializerMarkup = """
                                                 public sealed class C
                                                 {
                                                     public bool M()
                                                     {
                                                         var {|SST2266:empty|} = default(System.Collections.Generic.List<int>);
                                                         return empty is null;
                                                     }
                                                 }
                                                 """;

    /// <summary>The long initializer inlined into its one read.</summary>
    private const string LongInitializerFixed = """
                                                public sealed class C
                                                {
                                                    public bool M()
                                                    {
                                                        return default(System.Collections.Generic.List<int>) is null;
                                                    }
                                                }
                                                """;

    /// <summary>Verifies a pure single-use local is inlined into its one read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureLocalIsInlinedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly string _value = "x";

                                  public string M()
                                  {
                                      var {|SST2266:local|} = _value;
                                      return local;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private readonly string _value = "x";

                                       public string M()
                                       {
                                           return _value;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inlined operator initializer is parenthesized to keep its precedence.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperatorInitializerIsParenthesizedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a, int b)
                                  {
                                      var {|SST2266:sum|} = a + b;
                                      return sum * 2;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int a, int b)
                                       {
                                           return (a + b) * 2;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inlined operator initializer keeps its parentheses inside an argument list.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperatorInitializerIsNotParenthesizedInAnArgumentAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a, int b)
                                  {
                                      var {|SST2266:sum|} = a + b;
                                      return Use(sum);
                                  }

                                  private static int Use(int value) => value;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int a, int b)
                                       {
                                           return Use(a + b);
                                       }

                                       private static int Use(int value) => value;
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inlined operator initializer takes no parentheses in a return statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperatorInitializerIsNotParenthesizedInAReturnAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a, int b)
                                  {
                                      var {|SST2266:sum|} = a + b;
                                      return sum;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int a, int b)
                                       {
                                           return a + b;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inlined operator initializer takes no parentheses in another initializer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperatorInitializerIsNotParenthesizedInAnInitializerAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a, int b)
                                  {
                                      var {|SST2266:sum|} = a + b;
                                      var total = sum;
                                      return total + total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int a, int b)
                                       {
                                           var total = a + b;
                                           return total + total;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inlined operator initializer takes no parentheses on the right of an assignment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperatorInitializerIsNotParenthesizedInAnAssignmentAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a, int b)
                                  {
                                      var total = 0;
                                      var {|SST2266:sum|} = a + b;
                                      total = sum;
                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int a, int b)
                                       {
                                           var total = 0;
                                           total = a + b;
                                           return total;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inlined operator initializer takes no parentheses inside existing ones.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperatorInitializerIsNotParenthesizedInsideParenthesesAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a, int b)
                                  {
                                      var {|SST2266:sum|} = a + b;
                                      return (sum);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int a, int b)
                                       {
                                           return (a + b);
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local whose reads cannot all be counted is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The second read is inside an inactive <c>#if</c> region, so it is trivia rather than a node. Inlining
    /// on the strength of the one visible read would break every other configuration of the file.
    /// </remarks>
    [Test]
    public async Task LocalAlsoReadFromAnInactiveRegionIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M()
                                  {
                                      var value = _text;
                              #if SOME_FLAG
                                      return value + value;
                              #else
                                      return value;
                              #endif
                                  }

                                  private readonly string _text = "x";
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a local whose initializer has side effects is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImpureInitializerIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      var value = Compute();
                                      return value;
                                  }

                                  private static int Compute() => 1;
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a local read more than once is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalReadTwiceIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a)
                                  {
                                      var value = a;
                                      return value + value;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a use that does not immediately follow the declaration is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAdjacentUseIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a)
                                  {
                                      var value = a;
                                      System.Console.WriteLine();
                                      return value;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a local captured by a lambda is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturedLocalIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public System.Func<int> M(int a)
                                  {
                                      var value = a;
                                      return () => value;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a use preceded by a side effect in the same statement is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SideEffectBeforeUseIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a)
                                  {
                                      var value = a;
                                      return Side() + value;
                                  }

                                  private static int Side() => 0;
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a local that widens its initializer to another type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The declaration is what applies the conversion. Inlining drops it, so the use site binds against the
    /// initializer's own type — which may declare members that shadow the ones the widened type reaches.
    /// </remarks>
    [Test]
    public async Task WideningDeclarationIsCleanAsync()
    {
        const string Source = """
                              public interface IBase
                              {
                              }

                              public interface IDerived : IBase
                              {
                              }

                              public sealed class C
                              {
                                  public void M(IDerived builder)
                                  {
                                      IBase widened = builder;
                                      Use(widened);
                                  }

                                  private static void Use(IBase value)
                                  {
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a local whose initializer is a method group is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A method group has no type of its own — the declaration picks the delegate it converts to. Handing the
    /// bare group to an overloaded target re-runs that choice against a different candidate set.
    /// </remarks>
    [Test]
    public async Task MethodGroupInitializerIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      System.Func<int> action = Get;
                                      Use(action);
                                  }

                                  private static int Get() => 1;

                                  private static void Use(System.Func<int> value)
                                  {
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies an inferred local over a method group is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InferredMethodGroupInitializerIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      var action = Get;
                                      Use(action);
                                  }

                                  private static int Get() => 1;

                                  private static void Use(System.Func<int> value)
                                  {
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a local that boxes its initializer is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoxingDeclarationIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int _number = 1;

                                  public void M()
                                  {
                                      object boxed = _number;
                                      Use(boxed);
                                  }

                                  private static void Use(object value)
                                  {
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a target-typed <c>default</c> initializer is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The declaration is what gives a bare <c>default</c> its type. Spliced into an overloaded call it has
    /// none, and the call no longer compiles.
    /// </remarks>
    [Test]
    public async Task TargetTypedDefaultIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      int zero = default;
                                      Use(zero);
                                  }

                                  private static void Use(int value)
                                  {
                                  }

                                  private static void Use(string value)
                                  {
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a declaration that is the last statement in its block is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>There is no following statement for the read to be in, so there is nothing to inline into.</remarks>
    [Test]
    public async Task DeclarationLastInItsBlockIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int a)
                                  {
                                      if (a > 0)
                                      {
                                          var value = a;
                                      }

                                      return a;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies an initializer wider than the default threshold is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LongInitializerIsCleanByDefaultAsync() => await VerifyCleanAsync(LongInitializerSource);

    /// <summary>Verifies a raised rule-specific threshold reports an initializer the default would keep.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredThresholdReportsALongInitializerAsync()
        => await RunAsync(LongInitializerMarkup, LongInitializerFixed, "stylesharp.SST2266.max_initializer_length = 80");

    /// <summary>Verifies the project-wide threshold key is honoured when the rule-specific one is unset.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProjectWideThresholdReportsALongInitializerAsync()
        => await RunAsync(LongInitializerMarkup, LongInitializerFixed, "stylesharp.max_initializer_length = 80");

    /// <summary>Verifies the rule-specific threshold wins over the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificThresholdOverridesTheProjectWideOneAsync()
    {
        const string Options = """
                               stylesharp.SST2266.max_initializer_length = 80
                               stylesharp.max_initializer_length = 10
                               """;
        await RunAsync(LongInitializerMarkup, LongInitializerFixed, Options);
    }

    /// <summary>Verifies a non-numeric threshold keeps the default rather than disabling the rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonNumericThresholdKeepsTheDefaultAsync()
        => await VerifyCleanAsync(LongInitializerSource, "stylesharp.SST2266.max_initializer_length = wide");

    /// <summary>Verifies a non-positive threshold keeps the default rather than silencing every declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPositiveThresholdKeepsTheDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly string _value = "x";

                                  public string M()
                                  {
                                      var {|SST2266:local|} = _value;
                                      return local;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private readonly string _value = "x";

                                       public string M()
                                       {
                                           return _value;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, "stylesharp.SST2266.max_initializer_length = 0");
    }

    /// <summary>Verifies a local whose one read sits in a foreach body keeps its declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEachBodyReadKeepsTheLocalAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private System.Collections.Generic.List<int> Items => new();

                                  public int M(int[] values)
                                  {
                                      var total = 0;
                                      var count = Items.Count;
                                      foreach (var value in values)
                                      {
                                          total += count;
                                      }

                                      return total;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a local whose one read sits in a while body keeps its declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WhileBodyReadKeepsTheLocalAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private System.Collections.Generic.List<int> Items => new();

                                  public int M(int limit)
                                  {
                                      var total = 0;
                                      var count = Items.Count;
                                      while (total < limit)
                                      {
                                          total += count;
                                      }

                                      return total;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a literal initializer is still inlined into a loop, because re-reading it costs nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralInitializerIsInlinedIntoALoopAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int[] values)
                                  {
                                      var total = 0;
                                      var {|SST2266:step|} = 2;
                                      foreach (var value in values)
                                      {
                                          total += step;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int[] values)
                                       {
                                           var total = 0;
                                           foreach (var value in values)
                                           {
                                               total += 2;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="options">Extra <c>.editorconfig</c> lines, when the defaults are not wanted.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource, string? options = null)
    {
        var test = CreateTest(source, options);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <param name="options">Extra <c>.editorconfig</c> lines, when the defaults are not wanted.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? options = null)
    {
        var test = CreateTest(source, options);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2266 enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="options">Extra <c>.editorconfig</c> lines, when the defaults are not wanted.</param>
    /// <returns>The configured test.</returns>
    private static VerifyInlineSingleUseLocal.Test CreateTest(string source, string? options)
    {
        var test = new VerifyInlineSingleUseLocal.Test
        {
            TestCode = source,
        };

        var config = $"""
                      root = true

                      [*.cs]
                      dotnet_diagnostic.SST2266.severity = warning
                      {options}
                      """;

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
