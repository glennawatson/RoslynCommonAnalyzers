// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

using VerifyModernSyntaxValue = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModernSyntaxValueAnalyzer,
    StyleSharp.Analyzers.ModernSyntaxValueCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for value-oriented modern syntax rules (SST2220-SST2233).</summary>
public class ModernSyntaxValueAnalyzerUnitTest
{
    /// <summary>Verifies a redundant ToString call is folded into the interpolation hole.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ToStringInsideInterpolationIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int value) => $"Value: {{|SST2220:value.ToString("X")|}}";
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(int value) => $"Value: {value:X}";
                                   }
                                   """;

        await VerifyModernSyntaxValue.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an intentionally ignored return value can be made explicit when the rule is enabled.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IgnoredExpressionValueIsAssignedToDiscardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      {|SST2221:Compute()|};
                                  }

                                  private int Compute() => 1;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           _ = Compute();
                                       }

                                       private int Compute() => 1;
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);
        Enable(test, "SST2221");

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies duplicate ignored-value diagnostics register only one batch edit.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IgnoredExpressionValueDuplicateDiagnosticsAreDeduplicatedAsync()
    {
        var descriptor = ModernSyntaxRules.MakeIgnoredExpressionValueExplicit;
        var first = Diagnostic.Create(descriptor, Location.Create("Test0.cs", new TextSpan(42, 24), default));
        var duplicate = Diagnostic.Create(descriptor, Location.Create("Test0.cs", new TextSpan(42, 24), default));
        var second = Diagnostic.Create(descriptor, Location.Create("Test0.cs", new TextSpan(120, 24), default));

        var diagnostics = ImmutableArray.Create(first, duplicate, second);
        var unique = BatchEditFixAllProvider.UniqueDiagnostics(diagnostics).ToArray();

        await Assert.That(unique).Count().IsEqualTo(2);
        await Assert.That(unique[0]).IsSameReferenceAs(first);
        await Assert.That(unique[1]).IsSameReferenceAs(second);
    }

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

    /// <summary>Verifies a null fallback assignment is rewritten with coalescing assignment.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullFallbackAssignmentUsesCoalesceAssignmentAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      string value = null;
                                      {|SST2223:if|} (value is null)
                                      {
                                          value = "fallback";
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           string value = null;
                                           value ??= "fallback";
                                       }
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an anonymous object can become a tuple literal when the opt-in rule is enabled.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AnonymousObjectCanBecomeTupleLiteralAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public object M(int id, string name) => {|SST2224:new|} { id, Label = name };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public object M(int id, string name) => (id, Label: name);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);
        Enable(test, "SST2224");

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies foreach loops over strongly typed object sources show the cast at the source expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ForeachElementCastIsMadeVisibleAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(object[] values)
                                  {
                                      {|SST2225:foreach|} (string value in values)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(object[] values)
                                       {
                                           foreach (string value in System.Linq.Enumerable.Cast<string>(values))
                                           {
                                           }
                                       }
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a source cast that hides another explicit conversion receives the inner cast.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HiddenInnerCastIsAddedAsync()
    {
        const string Source = """
                              public class Base { }
                              public sealed class Derived : Base { }

                              public sealed class Castable
                              {
                                  public static explicit operator Base(Castable value) => new Base();
                              }

                              public sealed class C
                              {
                                  public Derived M(Castable value) => {|SST2226:(Derived)value|};
                              }
                              """;
        const string FixedSource = """
                                   public class Base { }
                                   public sealed class Derived : Base { }

                                   public sealed class Castable
                                   {
                                       public static explicit operator Base(Castable value) => new Base();
                                   }

                                   public sealed class C
                                   {
                                       public Derived M(Castable value) => (Derived)(Base)value;
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a post-assignment null throw is folded into the assigned expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullThrowAfterAssignmentIsFoldedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public string M(string input)
                                  {
                                      string value = input;
                                      {|SST2227:if|} (value == null)
                                      {
                                          throw new InvalidOperationException();
                                      }

                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public string M(string input)
                                       {
                                           string value = input ?? throw new InvalidOperationException();

                                           return value;
                                       }
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a delegate local used only as a direct call becomes a local function.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DelegateLocalBecomesLocalFunctionAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M()
                                  {
                                      Func<int, int> {|SST2228:add|} = x => x + 1;
                                      return add(1);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           int add(int x) => x + 1;
                                           return add(1);
                                       }
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a Where predicate is carried by the terminal LINQ call.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereAnyChainIsCollapsedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public bool M(int[] values) => values.Where(value => value > 0).{|SST2229:Any|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public bool M(int[] values) => values.Any(value => value > 0);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a LINQ type check followed by Cast is represented as one typed filter.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereTypeCheckCastChainUsesOneTypedFilterAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(object[] values) => values.Where(value => value is string).{|SST2230:Cast<string>|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public object M(object[] values) => values.OfType<string>();
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies broad object patterns become direct null patterns.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ObjectPatternBecomesNullPatternAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(object value) => value is {|SST2231:object|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(object value) => value is not null;
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies concrete generic arguments are omitted inside nameof.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NameofGenericTypeOmitsArgumentsAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M() => {|SST2232:nameof|}(System.Collections.Generic.List<int>);
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M() => nameof(System.Collections.Generic.List<>);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies hot-path projects can opt into LINQ method diagnostics.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HotPathLinqCallIsReportedWhenEnabledAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.{|SST2233:Select|}(value => value + 1);
                              }
                              """;
        var test = CreateNet80Test(Source);
        Enable(test, "SST2233");

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a .NET 8 verifier test.</summary>
    /// <param name="source">The source.</param>
    /// <param name="fixedSource">The optional fixed source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyModernSyntaxValue.Test CreateNet80Test(string source, string? fixedSource = null)
    {
        var test = new VerifyModernSyntaxValue.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        AddModernParseOptions(test);
        return test;
    }

    /// <summary>Ensures feature-gated syntax rules run against the newest parser available to the test SDK.</summary>
    /// <param name="test">The test to configure.</param>
    private static void AddModernParseOptions(VerifyModernSyntaxValue.Test test)
        => test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var projectParseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, projectParseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

    /// <summary>Enables a disabled-by-default diagnostic for a verifier test.</summary>
    /// <param name="test">The verifier test.</param>
    /// <param name="diagnosticId">The diagnostic id.</param>
    private static void Enable(VerifyModernSyntaxValue.Test test, string diagnosticId)
    {
        var extraOptions = diagnosticId == "SST2233"
            ? "stylesharp.avoid_linq_on_hot_path = true"
            : string.Empty;
        var config = $$"""
                       root = true

                       [*.cs]
                       dotnet_diagnostic.{{diagnosticId}}.severity = warning
                       {{extraOptions}}
                       """;
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
    }
}
