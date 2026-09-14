// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using AnalyzeModernSyntaxPreference = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.ModernSyntaxPreferenceAnalyzer>;

using VerifyModernSyntaxPreference = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModernSyntaxPreferenceAnalyzer,
    StyleSharp.Analyzers.ModernSyntaxPreferenceCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for compact modern syntax preference rules (SST2218-SST2219).</summary>
public class ModernSyntaxPreferenceAnalyzerUnitTest
{
    /// <summary>Records that overloads on sibling interfaces are missed when only the declaring interface is inspected.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SiblingInterfaceOverloadsAreReportedAsync() =>
        AnalyzeModernSyntaxPreference.VerifyAnalyzerAsync(
            """
            using System;
            interface IFirst { void Invoke(Action<int> action); }
            interface ISecond { void Invoke(Action<string> action); }
            interface ICombined : IFirst, ISecond { }
            class C
            {
                void M(ICombined value) { value.Invoke({|SST2218:(int item)|} => { }); }
            }
            """);

    /// <summary>Verifies an interface contract can be rebound without changing the selected implementation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImplementedInterfaceMethodKeepsBindingAsync() =>
        AnalyzeModernSyntaxPreference.VerifyAnalyzerAsync(
            """
            using System;
            interface I { void Invoke(Action<int> action); }
            class C : I
            {
                public void Invoke(Action<int> action) { }
                void M() { Invoke({|SST2218:(int item)|} => { }); }
            }
            """);

    /// <summary>Verifies unrelated interface members and hidden non-method members do not count as call overloads.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedInterfaceAndBasePropertyAllowSimplificationAsync() =>
        AnalyzeModernSyntaxPreference.VerifyAnalyzerAsync(
            """
            using System;
            interface I { void Other(); }
            class B { protected int Invoke { get; } }
            class C : B, I
            {
                public void Other() { }
                public new void Invoke(Action<int> action) { }
                void M() { Invoke({|SST2218:(int item)|} => { }); }
            }
            """);

    /// <summary>Verifies explicit generic arguments and multiple parameters survive overload-safe rewriting.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitGenericArgumentsAndConstructorTargetsAreFixedAsync() =>
        CreateNet80Test(
            """
            using System;
            class C
            {
                C(Func<int, int, int> combine) { }
                static void Invoke<T>(Action<T> action) { }
                void M()
                {
                    Invoke<int>({|SST2218:(int value)|} => { });
                    C.Invoke<int>({|SST2218:(int value)|} => { });
                    var instance = new C({|SST2218:(int left, int right)|} => left + right);
                }
            }
            """,
            """
            using System;
            class C
            {
                C(Func<int, int, int> combine) { }
                static void Invoke<T>(Action<T> action) { }
                void M()
                {
                    Invoke<int>((value) => { });
                    C.Invoke<int>((value) => { });
                    var instance = new C((left, right) => left + right);
                }
            }
            """).RunAsync(CancellationToken.None);

    /// <summary>Verifies constructor overload selection can depend on explicit parameter types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverloadedConstructorKeepsExplicitTypesAsync() =>
        AnalyzeModernSyntaxPreference.VerifyAnalyzerAsync(
            """
            using System;
            class C
            {
                C(Action<int> action) { }
                C(Action<string> action) { }
                void M() { var instance = new C((int value) => { }); }
            }
            """);

    /// <summary>Verifies both reduced and static extension invocations keep their binding after simplification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExtensionInvocationsPreserveBindingAsync() =>
        AnalyzeModernSyntaxPreference.VerifyAnalyzerAsync(
            """
            using System;
            static class Extensions
            {
                public static void Invoke(this string value, Action<int> action) { }
            }
            class C
            {
                void M()
                {
                    "".Invoke({|SST2218:(int value)|} => { });
                    Extensions.Invoke("", {|SST2218:(int value)|} => { });
                }
            }
            """);

    /// <summary>Verifies a local function is rebound independently of a same-named ordinary member.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalFunctionIgnoresOrdinaryMemberOverloadsAsync() =>
        AnalyzeModernSyntaxPreference.VerifyAnalyzerAsync(
            """
            using System;
            class C
            {
                void Invoke(Action<string> action) { }
                void M()
                {
                    void Invoke(Action<int> action) { }
                    Invoke({|SST2218:(int value)|} => { });
                }
            }
            """);

    /// <summary>Verifies function-pointer invocations expose no selected method symbol and stay unsimplified.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FunctionPointerInvocationHasNoMethodSymbolAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            using System;
            unsafe class C
            {
                void M(delegate*<Action<int>, void> invoke)
                {
                    invoke((int value) => { });
                }
            }
            """);
        var compilation = CSharpCompilation.Create(
            "FunctionPointerTarget",
            [tree],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        await Assert.That(compilation.GetDiagnostics()).IsEmpty();
        await Assert.That(compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol).IsNull();

        var diagnostics = await compilation.WithAnalyzers([new ModernSyntaxPreferenceAnalyzer()]).GetAnalyzerDiagnosticsAsync();

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies malformed getter returns and setter returns are not treated as expression bodies.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AccessorsWithoutSupportedStatementShapesAreCleanAsync() =>
        new AnalyzeModernSyntaxPreference.Test
        {
            TestCode = "class C { int Value { get { return; } set { return 1; } } int Other { get { value = 1; } set { } } }",
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Records that a naturally inferred delegate currently receives the parameter-type suggestion.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NaturallyInferredDelegateIsReportedAsync() =>
        AnalyzeModernSyntaxPreference.VerifyAnalyzerAsync(
            """
            class C
            {
                void M() { var identity = {|SST2218:(int value)|} => value; }
            }
            """);

    /// <summary>Verifies a named delegate declaration supplies the removable lambda parameter type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedDelegateTargetSuppliesParameterTypesAsync() =>
        new AnalyzeModernSyntaxPreference.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net80,
            TestCode = """
                class C
                {
                    delegate int Converter(int value);
                    Converter convert = {|SST2218:(int value)|} => value;
                    Converter Convert { get; } = {|SST2218:(int value)|} => value;
                    (Converter Convert, int Version) pair = ({|SST2218:(int value)|} => value, 1);
                    C() : this({|SST2218:(int value)|} => value) { }
                    C(Converter converter) { }
                }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies an indexer argument can use the delegate type supplied by its parameter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IndexerArgumentHasAnImplicitLambdaTargetAsync() =>
        AnalyzeModernSyntaxPreference.VerifyAnalyzerAsync(
            """
            using System;
            class C
            {
                int this[Func<int, int> transform] => transform(1);
                int M() => this[{|SST2218:(int value)|} => value];
            }
            """);

    /// <summary>Verifies empty, implicit, attributed, defaulted and modified parameter lists stay explicit.</summary>
    /// <param name="expression">The lambda source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("() => 1")]
    [Arguments("(value) => value")]
    [Arguments("([System.Obsolete] int value) => value")]
    [Arguments("(int value = 1) => value")]
    [Arguments("(ref int value) => value")]
    public Task NonRemovableLambdaParametersAreCleanAsync(string expression) =>
        new AnalyzeModernSyntaxPreference.Test { TestCode = $$"""class C { void M() { var lambda = {{expression}}; } }""", CompilerDiagnostics = CompilerDiagnostics.None }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies failed overload resolution and a lambda without any target stay silent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnboundLambdaAndUnresolvedInvocationAreCleanAsync() =>
        new AnalyzeModernSyntaxPreference.Test
        {
            TestCode = """
                using System;
                class C
                {
                    void Invoke(Action<int> action, int value) { }
                    void Invoke(Action<int> action, string value) { }
                    void M()
                    {
                        Missing((int value) => value);
                        Invoke((int value) => { }, default);
                        (int value) => value;
                    }
                }
                """,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies assignments in init accessors are fixed and non-expression accessor bodies remain unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InitAssignmentIsFixedWhileOtherAccessorStatementsStayAsync() =>
        CreateNet80Test(
            """
            namespace System.Runtime.CompilerServices { public class IsExternalInit { } }
            class C
            {
                int field;
                int Value { get => field; {|SST2219:init|} { field = value; } }
                int Other { get { throw new System.Exception(); } set { Consume(value); } }
                int Empty { get; set; }
                void Consume(int value) { }
            }
            """,
            """
            namespace System.Runtime.CompilerServices { public class IsExternalInit { } }
            class C
            {
                int field;
                int Value { get => field; init => field = value; }
                int Other { get { throw new System.Exception(); } set { Consume(value); } }
                int Empty { get; set; }
                void Consume(int value) { }
            }
            """).RunAsync(CancellationToken.None);

    /// <summary>Verifies explicit lambda parameter types are removed when the delegate target supplies them.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitLambdaParameterTypesAreFixedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private Func<int, int, int> _sum = {|SST2218:(int left, int right)|} => left + right;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       private Func<int, int, int> _sum = (left, right) => left + right;
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicit lambda parameter types are removed for single non-generic invocation targets.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitLambdaParameterTypeForSingleInvocationTargetIsFixedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M() => Select({|SST2218:(int value)|} => value + 1, 1);

                                  private static int Select(Func<int, int> selector, int value) => selector(value);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public int M() => Select((value) => value + 1, 1);

                                       private static int Select(Func<int, int> selector, int value) => selector(value);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicit lambda parameter types are kept when they select an overload.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitLambdaParameterTypeNeededForOverloadResolutionIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      Invoke((int _) => { });
                                  }

                                  private static void Invoke(Action<int> action)
                                  {
                                  }

                                  private static void Invoke(Action<string> action)
                                  {
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicit lambda parameter types are kept when they infer generic arguments.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitLambdaParameterTypeNeededForGenericInferenceIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      Create((string input) => input.ToUpperInvariant());
                                      C.Create((string input) => input.ToUpperInvariant());
                                  }

                                  private static void Create<T>(Func<T, string> action)
                                  {
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies simple get and set accessor bodies are expression-bodied.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SimplePropertyAccessorsAreFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public int Value
                                  {
                                      {|SST2219:get|} { return _value; }
                                      {|SST2219:set|} { _value = value; }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private int _value;

                                       public int Value
                                       {
                                           get => _value;
                                           set => _value = value;
                                       }
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies ambiguous syntax shapes stay clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AmbiguousSyntaxShapesAreCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private delegate int RefFunc(ref int value);

                                  private RefFunc _converted = (ref int value) => value;

                                  public int Value
                                  {
                                      get
                                      {
                                          var value = 1;
                                          return value;
                                      }
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies expression-bodied accessor suggestions stay silent below C# 7, where they cannot be written.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SimplePropertyAccessorsAreSilentBelowCSharp7Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public int Value
                                  {
                                      get { return _value; }
                                      set { _value = value; }
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxPreference.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a get-only property's sole block-bodied getter is left to the whole-member expression-body rule.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetOnlyPropertyDefersToWholeMemberExpressionBodyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public int Value
                                  {
                                      get { return _value; }
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a get-only indexer's sole block-bodied getter is left to the whole-member expression-body rule.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetOnlyIndexerDefersToWholeMemberExpressionBodyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int[] _items = new int[4];

                                  public int this[int index]
                                  {
                                      get { return _items[index]; }
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicit lambda parameter types reached through a conditional access are left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Detaching the invocation to rebind it without the explicit types speculatively orphans the
    /// conditional-access binding and crashes the binder, so the rule stays silent on the
    /// <c>receiver?.M(...)</c> form.
    /// </remarks>
    [Test]
    public async Task ConditionalAccessExplicitLambdaParameterTypesAreLeftAloneAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class C
                              {
                                  public void Use(List<string> items)
                                  {
                                      var result = items?.Where((string x) => x.Length > 0);
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a .NET 8 verifier test.</summary>
    /// <param name="source">The source.</param>
    /// <param name="fixedSource">The fixed source.</param>
    /// <returns>The configured test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VerifyModernSyntaxPreference.Test CreateNet80Test(string source, string fixedSource) =>
        new() { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = source, FixedCode = fixedSource };
}
