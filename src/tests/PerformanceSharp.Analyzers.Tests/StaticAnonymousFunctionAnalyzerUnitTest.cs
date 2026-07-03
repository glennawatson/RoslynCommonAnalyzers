// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyStaticFunction = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1000StaticAnonymousFunctionAnalyzer,
    PerformanceSharp.Analyzers.Psh1000StaticAnonymousFunctionCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1000 (anonymous functions without captures should be static) and its code fix.</summary>
public class StaticAnonymousFunctionAnalyzerUnitTest
{
    /// <summary>Verifies a non-capturing parenthesized lambda is reported (PSH1000) and made static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonCapturingParenthesizedLambdaMadeStaticAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<int> M() => {|PSH1000:()|} => 1;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.Func<int> M() => static () => 1;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a non-capturing simple lambda is reported (PSH1000) and made static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonCapturingSimpleLambdaMadeStaticAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<int, int> M() => {|PSH1000:x|} => 42;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.Func<int, int> M() => static x => 42;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a non-capturing anonymous method is reported (PSH1000) and made static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnonymousMethodMadeStaticAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<int> M() => {|PSH1000:delegate|} { return 1; };
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.Func<int> M() => static delegate { return 1; };
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the fix inserts <c>static</c> before an existing <c>async</c> modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncLambdaKeepsAsyncModifierAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<System.Threading.Tasks.Task<int>> M() => async {|PSH1000:()|} => await System.Threading.Tasks.Task.FromResult(1);
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.Func<System.Threading.Tasks.Task<int>> M() => static async () => await System.Threading.Tasks.Task.FromResult(1);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a lambda using only its own parameter is still capture-free and reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaUsingOwnParameterOnlyReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<int, int> M() => {|PSH1000:x|} => x * 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.Func<int, int> M() => static x => x * 2;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Fix All makes every capture-free anonymous function static in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<int> M1() => {|PSH1000:()|} => 1;

                                  public System.Func<int> M2() => {|PSH1000:()|} => 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.Func<int> M1() => static () => 1;

                                       public System.Func<int> M2() => static () => 2;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a lambda capturing a local variable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaCapturingLocalIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public System.Func<int> M()
                {
                    var local = 5;
                    return () => local;
                }
            }
            """);

    /// <summary>Verifies a lambda capturing an enclosing method parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaCapturingParameterIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public System.Func<int> M(int value) => () => value;
            }
            """);

    /// <summary>Verifies a lambda calling an instance method (capturing <c>this</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaCallingInstanceMethodIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public System.Func<int> M() => () => GetValue();

                private int GetValue() => 3;
            }
            """);

    /// <summary>Verifies an anonymous function that is already static is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlreadyStaticLambdaIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public System.Func<int> M() => static () => 1;
            }
            """);

    /// <summary>Verifies a lambda converted to an expression tree is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionTreeLambdaIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public System.Linq.Expressions.Expression<System.Func<int>> M() => () => 1;
            }
            """);

    /// <summary>Verifies the rule stays silent for files parsed as C# 8, where the modifier does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBeforeCSharp9Async()
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<int> M() => () => 1;
                              }
                              """;

        var test = new VerifyStaticFunction.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
            FixedCode = Source
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp8));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyStaticFunction.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90CleanAsync(string source)
        => await VerifyNet90Async(source, source);
}
