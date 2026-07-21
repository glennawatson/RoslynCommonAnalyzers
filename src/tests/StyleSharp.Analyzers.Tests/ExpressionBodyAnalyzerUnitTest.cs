// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionBodyAnalyzer,
    StyleSharp.Analyzers.ExpressionBodyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="ExpressionBodyAnalyzer"/> and <see cref="ExpressionBodyCodeFixProvider"/>, which
/// collapse a single-statement block body to an expression body across seven member kinds (SST2275-SST2281).
/// The constructor, operator, and conversion-operator ids ship disabled, so their tests enable the id first.
/// </summary>
public class ExpressionBodyAnalyzerUnitTest
{
    /// <summary>Verifies a value-returning method's single-return block becomes an expression body (SST2275).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodReturningExpressionIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int {|SST2275:Sum|}(int a, int b)
                                  {
                                      return a + b;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int Sum(int a, int b) => a + b;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a void method's single expression statement becomes an expression body (SST2275).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VoidMethodWithSingleStatementIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void {|SST2275:Add|}(System.Collections.Generic.List<int> sink, int value)
                                  {
                                      sink.Add(value);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void Add(System.Collections.Generic.List<int> sink, int value) => sink.Add(value);
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method whose block has more than one statement is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiStatementMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int Sum(int a, int b)
                {
                    var total = a + b;
                    return total;
                }
            }
            """);

    /// <summary>Verifies a method that already has an expression body is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int Sum(int a, int b) => a + b;
            }
            """);

    /// <summary>Verifies a void method whose only statement is a bare return is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareReturnMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    return;
                }
            }
            """);

    /// <summary>Verifies a single-statement body that carries a comment is left alone so the comment is not lost.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodWithCommentInBodyIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int One()
                {
                    // keep this note
                    return 1;
                }
            }
            """);

    /// <summary>Verifies a comment sitting before the block's brace is carried onto the collapsed body.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentBeforeBraceIsPreservedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int {|SST2275:One|}() /* note */
                                  {
                                      return 1;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int One() /* note */ => 1;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a single-call constructor becomes an expression body when SST2276 is enabled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorWithSingleStatementIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public {|SST2276:C|}(int value)
                                  {
                                      Configure(value);
                                  }

                                  private void Configure(int value)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public C(int value) => Configure(value);

                                       private void Configure(int value)
                                       {
                                       }
                                   }
                                   """;
        await RunWithEnabledAsync(Source, FixedSource, ModernSyntaxRules.UseExpressionBodyForConstructor.Id);
    }

    /// <summary>Verifies a constructor with a base initializer is left alone even when SST2276 is enabled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorWithInitializerIsCleanAsync()
        => await RunAnalyzerWithEnabledAsync(
            """
            internal class B
            {
                protected B(int value)
                {
                }
            }

            internal class C : B
            {
                public C(int value) : base(value)
                {
                    Configure(value);
                }

                private void Configure(int value)
                {
                }
            }
            """,
            ModernSyntaxRules.UseExpressionBodyForConstructor.Id);

    /// <summary>Verifies the on-by-default nudges are Info and the contested member kinds ship disabled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RulePostureMatchesDesignAsync()
    {
        await Assert.That(ModernSyntaxRules.UseExpressionBodyForMethod.IsEnabledByDefault).IsTrue();
        await Assert.That(ModernSyntaxRules.UseExpressionBodyForMethod.DefaultSeverity).IsEqualTo(Microsoft.CodeAnalysis.DiagnosticSeverity.Info);
        await Assert.That(ModernSyntaxRules.UseExpressionBodyForProperty.IsEnabledByDefault).IsTrue();
        await Assert.That(ModernSyntaxRules.UseExpressionBodyForIndexer.IsEnabledByDefault).IsTrue();
        await Assert.That(ModernSyntaxRules.UseExpressionBodyForLocalFunction.IsEnabledByDefault).IsTrue();

        await Assert.That(ModernSyntaxRules.UseExpressionBodyForConstructor.IsEnabledByDefault).IsFalse();
        await Assert.That(ModernSyntaxRules.UseExpressionBodyForOperator.IsEnabledByDefault).IsFalse();
        await Assert.That(ModernSyntaxRules.UseExpressionBodyForConversionOperator.IsEnabledByDefault).IsFalse();
    }

    /// <summary>Verifies a single-return operator becomes an expression body when SST2277 is enabled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperatorWithSingleReturnIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal sealed class V
                              {
                                  public static V operator {|SST2277:+|}(V a, V b)
                                  {
                                      return new V();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal sealed class V
                                   {
                                       public static V operator +(V a, V b) => new V();
                                   }
                                   """;
        await RunWithEnabledAsync(Source, FixedSource, ModernSyntaxRules.UseExpressionBodyForOperator.Id);
    }

    /// <summary>Verifies a single-return conversion operator becomes an expression body when SST2278 is enabled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConversionOperatorWithSingleReturnIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal sealed class V
                              {
                                  public static implicit {|SST2278:operator|} int(V v)
                                  {
                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal sealed class V
                                   {
                                       public static implicit operator int(V v) => 0;
                                   }
                                   """;
        await RunWithEnabledAsync(Source, FixedSource, ModernSyntaxRules.UseExpressionBodyForConversionOperator.Id);
    }

    /// <summary>Verifies a get-only property's single-return getter becomes a whole-member expression body (SST2279).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetOnlyPropertyIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal sealed class C
                              {
                                  private int _value;

                                  public int {|SST2279:Value|}
                                  {
                                      get { return _value; }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal sealed class C
                                   {
                                       private int _value;

                                       public int Value => _value;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a property with a setter keeps its per-accessor shape (SST2279 stays out).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyWithSetterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal sealed class C
            {
                private int _value;

                public int Value
                {
                    get { return _value; }
                    set { _value = value; }
                }
            }
            """);

    /// <summary>Verifies a property whose getter already has an expression body is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedGetterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal sealed class C
            {
                private int _value;

                public int Value
                {
                    get => _value;
                }
            }
            """);

    /// <summary>Verifies a get accessor carrying an attribute is left alone, since the whole-member form cannot hold it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyWithAttributedGetterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            internal sealed class C
            {
                private int _value;

                public int Value
                {
                    [DebuggerStepThrough]
                    get { return _value; }
                }
            }
            """);

    /// <summary>Verifies a get-only indexer's single-return getter becomes a whole-member expression body (SST2280).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetOnlyIndexerIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal sealed class C
                              {
                                  private readonly int[] _items = new int[4];

                                  public int {|SST2280:this|}[int index]
                                  {
                                      get { return _items[index]; }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal sealed class C
                                   {
                                       private readonly int[] _items = new int[4];

                                       public int this[int index] => _items[index];
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an indexer with a setter keeps its per-accessor shape (SST2280 stays out).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexerWithSetterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal sealed class C
            {
                private readonly int[] _items = new int[4];

                public int this[int index]
                {
                    get { return _items[index]; }
                    set { _items[index] = value; }
                }
            }
            """);

    /// <summary>Verifies a value-returning local function's single-return block becomes an expression body (SST2281).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionReturningExpressionIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal sealed class C
                              {
                                  public int M()
                                  {
                                      return Double(2);

                                      int {|SST2281:Double|}(int n)
                                      {
                                          return n * 2;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal sealed class C
                                   {
                                       public int M()
                                       {
                                           return Double(2);

                                           int Double(int n) => n * 2;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a void local function's single expression statement becomes an expression body (SST2281).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VoidLocalFunctionWithSingleStatementIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal sealed class C
                              {
                                  public void M(System.Collections.Generic.List<int> sink)
                                  {
                                      Add(1);

                                      void {|SST2281:Add|}(int n)
                                      {
                                          sink.Add(n);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal sealed class C
                                   {
                                       public void M(System.Collections.Generic.List<int> sink)
                                       {
                                           Add(1);

                                           void Add(int n) => sink.Add(n);
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local function whose block has more than one statement is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiStatementLocalFunctionIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal sealed class C
            {
                public int M()
                {
                    return Double(2);

                    int Double(int n)
                    {
                        var doubled = n * 2;
                        return doubled;
                    }
                }
            }
            """);

    /// <summary>Runs a code-fix verification with the given disabled-by-default ids enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="enabledIds">The disabled-by-default ids to enable for the run.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunWithEnabledAsync(string source, string fixedSource, params string[] enabledIds)
    {
        var test = CreateEnabledTest(source, enabledIds);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the given disabled-by-default ids enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="enabledIds">The disabled-by-default ids to enable for the run.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAnalyzerWithEnabledAsync(string source, params string[] enabledIds)
    {
        var test = CreateEnabledTest(source, enabledIds);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with the given disabled-by-default ids enabled through <c>.editorconfig</c>.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="enabledIds">The disabled-by-default ids to enable for the run.</param>
    /// <returns>The configured test.</returns>
    private static Verify.Test CreateEnabledTest(string source, string[] enabledIds)
    {
        var builder = new System.Text.StringBuilder("root = true\n\n[*.cs]\n");
        for (var i = 0; i < enabledIds.Length; i++)
        {
            builder.Append("dotnet_diagnostic.").Append(enabledIds[i]).Append(".severity = warning\n");
        }

        var config = builder.ToString();
        var test = new Verify.Test { TestCode = source };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
