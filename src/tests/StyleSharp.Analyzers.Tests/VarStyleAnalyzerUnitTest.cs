// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyVarStyle = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2271VarStyleAnalyzer,
    StyleSharp.Analyzers.Sst2271VarStyleCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2271 (normalize the var-versus-explicit choice). The rule is disabled by default, so
/// every test enables it through an <c>.editorconfig</c> severity entry and, where relevant, sets the option.
/// </summary>
public class VarStyleAnalyzerUnitTest
{
    /// <summary>The <c>use_var</c> option value that asks for <c>var</c> on every local.</summary>
    private const string AlwaysUseVarStyle = "always";

    /// <summary>The option value that requires explicit type names.</summary>
    private const string NeverUseVarStyle = "never";

    /// <summary>Verifies declarations requiring a target type or an unsupported declaration shape retain their spelling.</summary>
    /// <param name="body">The declarations under analysis.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("int first = 1, second = 2;")]
    [Arguments("const int value = 1;")]
    [Arguments("int value;")]
    [Arguments("ref int value = ref input;")]
    [Arguments("int value = default;")]
    [Arguments("int[] values = [];")]
    [Arguments("string value = null;")]
    [Arguments("System.Span<int> values = flag ? System.Span<int>.Empty : stackalloc int[1];")]
    [Arguments("System.Span<int> values = flag switch { true => System.Span<int>.Empty, false => stackalloc int[1] };")]
    [Arguments("foreach (object value in new string[0]) { }")]
    [Arguments("foreach (var value in new int[0]) { }")]
    public Task DeclarationsThatCannotSafelyBecomeVarAreCleanAsync(string body) =>
        VerifyCleanAsync($"class C {{ void M(ref int input, bool flag) {{ {body} }} }}", AlwaysUseVarStyle, AnalyzerFrameworks.Net80);

    /// <summary>Verifies anonymous, tuple, and dynamic inferred types cannot be rewritten to an ordinary type name.</summary>
    /// <param name="body">The inferred declarations under analysis.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("var value = new { Name = 1 };")]
    [Arguments("var values = new[] { new { Name = 1 } };")]
    [Arguments("var value = (1, 2);")]
    [Arguments("var value = input;")]
    [Arguments("foreach (var value in new[] { new { Name = 1 } }) { }")]
    [Arguments("foreach (int value in new int[0]) { }")]
    public Task UnnameableInferredTypesAndExplicitLoopsAreCleanAsync(string body) =>
        VerifyCleanAsync($"class C {{ void M(dynamic input) {{ {body} }} }}", NeverUseVarStyle, AnalyzerFrameworks.Net80);

    /// <summary>Verifies conditionals and switches without stack allocation retain their natural type under var.</summary>
    /// <param name="initializer">The initializer that does not depend on a declared target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("flag ? 1 : 2")]
    [Arguments("flag switch { true => 1, false => 2 }")]
    [Arguments("(1)")]
    public Task NaturalInitializerTypeAllowsVarAsync(string initializer) =>
        RunAsync(
            $$"""class C { int M(bool flag) { {|SST2271:int|} value = {{initializer}}; return value; } }""",
            $$"""class C { int M(bool flag) { var value = {{initializer}}; return value; } }""",
            AlwaysUseVarStyle);

    /// <summary>Verifies each initializer shape is classified by whether its syntax names a type.</summary>
    /// <param name="expression">The initializer syntax.</param>
    /// <param name="expected">Whether the initializer makes its type obvious.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new C()", true)]
    [Arguments("new int[1]", true)]
    [Arguments("(int)value", true)]
    [Arguments("default(int)", true)]
    [Arguments("1", true)]
    [Arguments("null", false)]
    [Arguments("default", false)]
    [Arguments("Load()", false)]
    public async Task ObviousInitializerClassificationMatchesSyntaxAsync(string expression, bool expected)
    {
        var result = Sst2271VarStyleAnalyzer.IsObviousInitializer(SyntaxFactory.ParseExpression(expression));
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>Verifies name binding rejects missing names and names for another type.</summary>
    /// <param name="typeName">The proposed explicit type name.</param>
    /// <param name="expected">Whether the name resolves to the local's integer type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int", true)]
    [Arguments("string", false)]
    [Arguments("Missing", false)]
    public async Task ExplicitTypeNameMustBindToIntAsync(string typeName, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { int value = 1; } }");
        var compilation = CSharpCompilation.Create("TypeBinding", [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var root = await tree.GetRootAsync();
        var position = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Single().SpanStart;
        var result = Sst2271VarStyleAnalyzer.TypeNameBindsTo(compilation.GetSemanticModel(tree), position, typeName, compilation.GetSpecialType(SpecialType.System_Int32));
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>Verifies only the declaration's type node is resolved, including foreach element types.</summary>
    /// <param name="source">The document containing the type syntax.</param>
    /// <param name="target">The selected type or expression name.</param>
    /// <param name="expected">Whether the selected node represents an integer variable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int value; }", "int", false)]
    [Arguments("class C { void M() { int value = 1; } }", "int", true)]
    [Arguments("class C { void M(int[] values) { foreach (var value in values) { } } }", "var", true)]
    [Arguments("class C { void M(int[] values) { foreach (var value in values) { } } }", "values", false)]
    public async Task VariableTypeResolutionRequiresDeclarationTypeAsync(string source, string target, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("VariableType", [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var root = await tree.GetRootAsync();
        var node = root.DescendantNodes().OfType<TypeSyntax>().Single(type => type.ToString() == target);
        var resolved = Sst2271VarStyleAnalyzer.ResolveVariableType(compilation.GetSemanticModel(tree), node);
        await Assert.That(resolved?.SpecialType == SpecialType.System_Int32).IsEqualTo(expected);
        if (!expected)
        {
            await Assert.That(resolved).IsNull();
        }
    }

    /// <summary>Verifies unresolved locals and non-enumerable foreach inputs produce no style diagnostic.</summary>
    /// <param name="body">The incomplete declaration or loop.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("var value = missing;")]
    [Arguments("foreach (var value in 1) { }")]
    public async Task UnresolvedVariablesAreCleanAsync(string body)
    {
        var test = CreateTest($"class C {{ void M() {{ {body} }} }}", NeverUseVarStyle);
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an explicit local becomes <c>var</c> when the style is <c>always</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitLocalBecomesVarWhenAlwaysAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      {|SST2271:int|} value = 1;
                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           var value = 1;
                                           return value;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: AlwaysUseVarStyle);
    }

    /// <summary>Verifies a <c>var</c> local names its type when the style is <c>never</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VarLocalBecomesExplicitWhenNeverAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      {|SST2271:var|} value = 1;
                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           int value = 1;
                                           return value;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: NeverUseVarStyle);
    }

    /// <summary>Verifies an obvious explicit local becomes <c>var</c> under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObviousExplicitBecomesVarUnderDefaultAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      {|SST2271:List<int>|} numbers = new List<int>();
                                      numbers.Add(1);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           var numbers = new List<int>();
                                           numbers.Add(1);
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies a non-obvious <c>var</c> local names its type under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonObviousVarBecomesExplicitUnderDefaultAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  private static List<int> Load() => new List<int>();

                                  public void M()
                                  {
                                      {|SST2271:var|} numbers = Load();
                                      numbers.Add(1);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       private static List<int> Load() => new List<int>();

                                       public void M()
                                       {
                                           List<int> numbers = Load();
                                           numbers.Add(1);
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies an interface-typed local is never converted to <c>var</c>, which would change its type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceTypedLocalIsCleanWhenAlwaysAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      IList<int> numbers = new List<int>();
                                      numbers.Add(1);
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: AlwaysUseVarStyle);
    }

    /// <summary>Verifies a target-typed <c>new()</c> local is never converted to <c>var</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedNewLocalIsCleanWhenAlwaysAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      List<int> numbers = new();
                                      numbers.Add(1);
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: AlwaysUseVarStyle);
    }

    /// <summary>Verifies a <c>stackalloc</c> local is never converted to <c>var</c>, which would make it a pointer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StackAllocLocalIsCleanWhenAlwaysAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M()
                                  {
                                      Span<char> buffer = stackalloc char[32];
                                      return buffer.Length;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: AlwaysUseVarStyle, ReferenceAssemblies.Net.Net80);
    }

    /// <summary>Verifies an implicitly typed <c>stackalloc</c> local is never converted to <c>var</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitStackAllocLocalIsCleanWhenAlwaysAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M()
                                  {
                                      Span<int> values = stackalloc[] { 1, 2 };
                                      return values.Length;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: AlwaysUseVarStyle, ReferenceAssemblies.Net.Net80);
    }

    /// <summary>Verifies a conditional whose branches are <c>stackalloc</c> is never converted to <c>var</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalStackAllocLocalIsCleanWhenAlwaysAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M(bool small)
                                  {
                                      Span<char> buffer = small ? stackalloc char[4] : stackalloc char[32];
                                      return buffer.Length;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: AlwaysUseVarStyle, ReferenceAssemblies.Net.Net80);
    }

    /// <summary>Verifies a parenthesized <c>stackalloc</c> is never converted to <c>var</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedStackAllocLocalIsCleanWhenAlwaysAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M()
                                  {
                                      Span<char> buffer = (stackalloc char[32]);
                                      return buffer.Length;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: AlwaysUseVarStyle, ReferenceAssemblies.Net.Net80);
    }

    /// <summary>Verifies a <c>switch</c> expression over <c>stackalloc</c> arms is never converted to <c>var</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchExpressionStackAllocLocalIsCleanWhenAlwaysAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int M(bool small)
                                  {
                                      Span<char> buffer = small switch { true => stackalloc char[4], false => stackalloc char[32] };
                                      return buffer.Length;
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: AlwaysUseVarStyle, ReferenceAssemblies.Net.Net80);
    }

    /// <summary>Verifies a <c>default(T)</c> initializer, whose type does not depend on the target, still converts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitDefaultExpressionBecomesVarWhenAlwaysAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      {|SST2271:int|} value = default(int);
                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           var value = default(int);
                                           return value;
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: AlwaysUseVarStyle);
    }

    /// <summary>Verifies an explicit foreach variable becomes <c>var</c> when the style is <c>always</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitForEachBecomesVarWhenAlwaysAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int[] items)
                                  {
                                      foreach ({|SST2271:int|} item in items)
                                      {
                                          System.Console.WriteLine(item);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(int[] items)
                                       {
                                           foreach (var item in items)
                                           {
                                               System.Console.WriteLine(item);
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: AlwaysUseVarStyle);
    }

    /// <summary>Verifies a <c>var</c> foreach variable names its type when the style is <c>never</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VarForEachBecomesExplicitWhenNeverAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int[] items)
                                  {
                                      foreach ({|SST2271:var|} item in items)
                                      {
                                          System.Console.WriteLine(item);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(int[] items)
                                       {
                                           foreach (int item in items)
                                           {
                                               System.Console.WriteLine(item);
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: NeverUseVarStyle);
    }

    /// <summary>Verifies a foreach variable is left alone under the default when-obvious style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEachIsCleanUnderDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int[] items)
                                  {
                                      foreach (var item in items)
                                      {
                                          System.Console.WriteLine(item);
                                      }
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled and the given style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="style">The <c>use_var</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource, string? style)
    {
        var test = CreateTest(source, style);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <param name="style">The <c>use_var</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <param name="references">The reference assemblies to compile against, or <see langword="null"/> for the default set.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? style, ReferenceAssemblies? references = null)
    {
        var test = CreateTest(source, style);
        if (references is not null)
        {
            test.ReferenceAssemblies = references;
        }

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2271 enabled and an optional style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="style">The <c>use_var</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The configured test.</returns>
    private static VerifyVarStyle.Test CreateTest(string source, string? style)
    {
        var test = new VerifyVarStyle.Test { TestCode = source, };

        var config = "root = true\n\n[*.cs]\ndotnet_diagnostic.SST2271.severity = warning\n";
        if (style is not null)
        {
            config += $"stylesharp.use_var = {style}\n";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
