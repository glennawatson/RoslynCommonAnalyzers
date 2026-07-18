// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using VerifyModernSyntaxValue = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModernSyntaxValueAnalyzer,
    StyleSharp.Analyzers.ModernSyntaxValueCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the overwritten-value rule (SST2222): local writes that can never be read.</summary>
public class RemoveOverwrittenValueUnitTest
{
    /// <summary>Verifies adjacent overwritten local values are removed without touching the later write.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverwrittenLocalValueIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      int value = {|SST2222:0|};
                                      value = 1;
                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           int value;
                                           value = 1;
                                           return value;
                                       }
                                   }
                                   """;

        await VerifyModernSyntaxValue.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies repeated discard assignments are not treated as overwritten local values.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedDiscardAssignmentsAreCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      _ = 0;
                                      _ = 1;
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an initializer is preserved when the following assignment captures the local.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverwrittenLocalValueCapturedByFollowingAssignmentIsCleanAsync()
    {
        const string Source = """
                              #nullable enable

                              using System;

                              public sealed class C
                              {
                                  private readonly object _gate = new();
                                  private readonly IScheduler _scheduler;

                                  public C(IScheduler scheduler)
                                  {
                                      _scheduler = scheduler;
                                  }

                                  private void Reschedule()
                                  {
                                      var isAdded = false;
                                      var isDone = false;
                                      IDisposable? disposable = null;
                                      disposable = _scheduler.Schedule(() =>
                                      {
                                          lock (_gate)
                                          {
                                              if (isAdded)
                                              {
                                                  _ = Remove(disposable!);
                                              }
                                              else
                                              {
                                                  isDone = true;
                                              }
                                          }

                                          RunRecursiveAction();
                                      });

                                      lock (_gate)
                                      {
                                          if (!isDone)
                                          {
                                              Add(disposable);
                                              isAdded = true;
                                          }
                                      }
                                  }

                                  private void Add(IDisposable? disposable)
                                  {
                                  }

                                  private bool Remove(IDisposable disposable) => true;

                                  private void RunRecursiveAction()
                                  {
                                  }
                              }

                              public interface IScheduler
                              {
                                  IDisposable Schedule(Action action);
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a postfix step whose local dies at the enclosing return is removed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementDiscardedByReturnIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Increment(int seed)
                                  {
                                      int value = seed;
                                      return {|SST2222:value++|};
                                  }

                                  public int Decrement(int seed)
                                  {
                                      int value = seed;
                                      return {|SST2222:value--|};
                                  }

                                  public int Parenthesized(int seed)
                                  {
                                      int value = seed;
                                      return ({|SST2222:value++|});
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Increment(int seed)
                                       {
                                           int value = seed;
                                           return value;
                                       }

                                       public int Decrement(int seed)
                                       {
                                           int value = seed;
                                           return value;
                                       }

                                       public int Parenthesized(int seed)
                                       {
                                           int value = seed;
                                           return (value);
                                       }
                                   }
                                   """;

        await VerifyModernSyntaxValue.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an assignment that puts a local's original value back over its own postfix step is removed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SelfAssignedIncrementIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int seed)
                                  {
                                      int count = seed;
                                      {|SST2222:count = count++|};
                                      return count;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int seed)
                                       {
                                           int count = seed;
                                           return count;
                                       }
                                   }
                                   """;

        await VerifyModernSyntaxValue.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies steps on fields, parameters, and elements stay because their storage is not a dying local.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnOfNonLocalTargetIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _total;

                                  public int FromField()
                                  {
                                      return _total++;
                                  }

                                  public int FromParameter(int seed)
                                  {
                                      return seed++;
                                  }

                                  public int FromElement(int[] values)
                                  {
                                      return values[0]++;
                                  }

                                  public void FromNothing()
                                  {
                                      return;
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies steps whose written value is read afterwards stay in place.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StepWhoseValueIsReadIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int PrefixReturn(int seed)
                                  {
                                      int value = seed;
                                      return ++value;
                                  }

                                  public int PrefixAssignment(int seed)
                                  {
                                      int value = seed;
                                      value = ++value;
                                      return value;
                                  }

                                  public int CrossLocal(int seed)
                                  {
                                      int value = seed;
                                      int copy;
                                      copy = value++;
                                      return copy + value;
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a local captured by a lambda keeps its final step because the closure can read it later.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnOfCapturedLocalIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M(out Func<int> reader)
                                  {
                                      int value = 0;
                                      reader = () => value;
                                      return value++;
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a return inside a try keeps its step because the finally can still read the local.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnInsideTryIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _log;

                                  public int M(int seed)
                                  {
                                      int value = seed;
                                      try
                                      {
                                          return value++;
                                      }
                                      finally
                                      {
                                          _log = value;
                                      }
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a ref local keeps its step because the write lands in storage that outlives the frame.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnOfRefLocalIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _total;

                                  public int M()
                                  {
                                      ref int value = ref _total;
                                      return value++;
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a local whose reference was handed out keeps its step because an alias may read it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnOfRefAliasedLocalIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _sink;

                                  public int FromRefArgument()
                                  {
                                      int value = 0;
                                      Capture(ref value);
                                      return value++;
                                  }

                                  public int FromRefDeclaration()
                                  {
                                      int value = 0;
                                      ref int alias = ref value;
                                      alias = 2;
                                      return value++;
                                  }

                                  private void Capture(ref int target) => _sink = target;
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a local whose address was taken keeps its step because a pointer may read it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnOfAddressTakenLocalIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public unsafe int M()
                                  {
                                      int value = 0;
                                      int* pointer = &value;
                                      *pointer = 3;
                                      return value++;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxValue.Test { TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var options = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies steps that can throw on overflow stay because removal would change behavior.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverflowThrowingIncrementInReturnIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int CheckedStep(int seed)
                                  {
                                      int value = seed;
                                      checked
                                      {
                                          return value++;
                                      }
                                  }

                                  public decimal DecimalStep(decimal seed)
                                  {
                                      decimal value = seed;
                                      return value++;
                                  }

                                  public decimal? LiftedDecimalStep(decimal? seed)
                                  {
                                      decimal? value = seed;
                                      return value++;
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a compilation that checks overflow keeps every integral step.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnInCheckedCompilationIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int seed)
                                  {
                                      int value = seed;
                                      return value++;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxValue.Test { TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var options = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, options.WithOverflowChecks(true));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unchecked block restores the report inside a compilation that checks overflow.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UncheckedIncrementInCheckedCompilationIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int seed)
                                  {
                                      int value = seed;
                                      unchecked
                                      {
                                          return {|SST2222:value++|};
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int seed)
                                       {
                                           int value = seed;
                                           unchecked
                                           {
                                               return value;
                                           }
                                       }
                                   }
                                   """;
        var test = new VerifyModernSyntaxValue.Test { TestCode = Source, FixedCode = FixedSource };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var options = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, options.WithOverflowChecks(true));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a user-defined step operator keeps its call because it can do real work.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UserDefinedIncrementInReturnIsCleanAsync()
    {
        const string Source = """
                              public struct Counter
                              {
                                  public int Total;

                                  public static Counter operator ++(Counter counter) => new Counter { Total = counter.Total + 1 };
                              }

                              public sealed class C
                              {
                                  public Counter M()
                                  {
                                      Counter value = default(Counter);
                                      return value++;
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a local function returning a step of an outer local keeps it because the local outlives the call.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnOfOuterLocalFromLocalFunctionIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      int value = 0;
                                      return Step() + value;

                                      int Step()
                                      {
                                          return value++;
                                      }
                                  }
                              }
                              """;

        await VerifyModernSyntaxValue.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a top-level return keeps its step, which sits outside any reported function body.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInTopLevelReturnIsCleanAsync()
    {
        const string Source = """
                              int value = args.Length;
                              return value++;
                              """;
        var test = new VerifyModernSyntaxValue.Test { TestCode = Source, FixedCode = Source };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a lambda that reads a same-named field does not shield the dying local's step.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IncrementInReturnWithSameNamedFieldReadInLambdaIsRemovedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private int value;

                                  public int M(out Func<int> reader)
                                  {
                                      reader = () => this.value;
                                      int value = 1;
                                      return {|SST2222:value++|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       private int value;

                                       public int M(out Func<int> reader)
                                       {
                                           reader = () => this.value;
                                           int value = 1;
                                           return value;
                                       }
                                   }
                                   """;

        await VerifyModernSyntaxValue.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a stale overwritten-value diagnostic on a bare step statement has no fixer.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverwrittenValueFixOnBareIncrementStatementMakesNoChangeAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int seed)
                                  {
                                      int value = seed;
                                      value++;
                                      return value;
                                  }
                              }
                              """;
        using var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution.AddProject("TestProject", "TestProject", LanguageNames.CSharp);
        var document = project.AddDocument("Test0.cs", SourceText.From(Source));
        var root = await document.GetSyntaxRootAsync(CancellationToken.None);
        var postfix = root!.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(
            ModernSyntaxRules.RemoveOverwrittenValue,
            Location.Create("Test0.cs", postfix.Span, default));

        var updated = ModernSyntaxValueCodeFixProvider.Apply(document, root, diagnostic);
        var updatedRoot = await updated.GetSyntaxRootAsync(CancellationToken.None);

        await Assert.That(updatedRoot!.ToFullString()).IsEqualTo(Source);
    }
}
