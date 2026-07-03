// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using AnalyzerVerifyEmptyComparison = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1204EmptyStringComparisonAnalyzer>;
using VerifyEmptyComparison = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1204EmptyStringComparisonAnalyzer,
    PerformanceSharp.Analyzers.Psh1204EmptyStringComparisonCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1204 (test for empty strings by length) and its code fix.</summary>
public class EmptyStringComparisonAnalyzerUnitTest
{
    /// <summary>Verifies <c>==</c> against <c>""</c> is reported (PSH1204) and rewritten to a length pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityWithEmptyLiteralReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s is { Length: 0 };
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies <c>!=</c> against <c>""</c> is rewritten to a negated length pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InequalityWithEmptyLiteralReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:!=|} "";
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s is not { Length: 0 };
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a comparison against <c>string.Empty</c> is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringEmptyComparisonReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} string.Empty;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s is { Length: 0 };
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the rewrite targets the value operand when the empty literal is on the left.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyLiteralOnLeftReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string s)
                                      => "" {|PSH1204:==|} s;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s is { Length: 0 };
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the fix inside an <c>if</c> condition produces compiling output without extra parentheses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IfConditionFixCompilesAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string s)
                                  {
                                      if (s {|PSH1204:==|} "")
                                      {
                                          return 1;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(string s)
                                       {
                                           if (s is { Length: 0 })
                                           {
                                               return 1;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the fix inside a logical-and expression parenthesizes the pattern and compiles.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LogicalAndFixCompilesAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string s, bool flag)
                                      => flag && s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string s, bool flag)
                                           => flag && (s is { Length: 0 });
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a user-defined equality operator against <c>""</c> on a non-string operand is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedOperatorIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class Wrapper
            {
                public static bool operator ==(Wrapper left, string right) => right.Length == 0;

                public static bool operator !=(Wrapper left, string right) => right.Length != 0;

                public override bool Equals(object obj) => false;

                public override int GetHashCode() => 0;
            }

            public class C
            {
                public bool M(Wrapper wrapper) => wrapper == "";
            }
            """);

    /// <summary>Verifies a comparison inside a lambda converted to an expression tree is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionTreeLambdaIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public string Name { get; set; } = "x";

                public System.Linq.Expressions.Expression<System.Func<C, bool>> M()
                    => c => c.Name == "";
            }
            """);

    /// <summary>Verifies the diagnostic is still reported for files parsed as C# 8, where the fix's <c>not</c> pattern does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportedWithoutFixBeforeCSharp9Async()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;

        var test = new AnalyzerVerifyEmptyComparison.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
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
        var test = new VerifyEmptyComparison.Test
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
