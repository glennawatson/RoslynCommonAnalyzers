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
    /// <summary>The editorconfig line selecting the direct length test.</summary>
    private const string LengthStyleSetting = "performancesharp.PSH1204.empty_string_style = length";

    /// <summary>The editorconfig line selecting string.IsNullOrEmpty.</summary>
    private const string IsNullOrEmptyStyleSetting = "performancesharp.PSH1204.empty_string_style = is_null_or_empty";

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

    /// <summary>Verifies the length style rewrites to <c>s.Length == 0</c> when the operand cannot be null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthStyleOnNotNullOperandUsesLengthCheckAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s.Length == 0;
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, LengthStyleSetting);
    }

    /// <summary>Verifies the length style negates for <c>!=</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthStyleNegatesInequalityAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:!=|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s.Length != 0;
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, LengthStyleSetting);
    }

    /// <summary>Verifies the length style parenthesizes an operand that binds looser than member access.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthStyleParenthesizesCompoundOperandAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string a, string b)
                                      => a + b {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string a, string b)
                                           => (a + b).Length == 0;
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, LengthStyleSetting);
    }

    /// <summary>Verifies the length style falls back to the pattern when the operand may be null, because <c>s.Length</c> would throw.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthStyleFallsBackToPatternWhenOperandMayBeNullAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string? s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string? s)
                                           => s is { Length: 0 };
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, LengthStyleSetting);
    }

    /// <summary>Verifies the length style falls back to the pattern where nullable analysis is off, which proves nothing about null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthStyleFallsBackToPatternWithoutNullableContextAsync()
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
        await VerifyNet90WithConfigAsync(Source, FixedSource, LengthStyleSetting);
    }

    /// <summary>Verifies a nullable operand narrowed to not-null by an earlier check does get the length style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthStyleUsesFlowStateNotDeclarationAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string? s)
                                  {
                                      if (s is null)
                                      {
                                          return false;
                                      }

                                      return s {|PSH1204:==|} "";
                                  }
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string? s)
                                       {
                                           if (s is null)
                                           {
                                               return false;
                                           }

                                           return s.Length == 0;
                                       }
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, LengthStyleSetting);
    }

    /// <summary>Verifies the is_null_or_empty style rewrites to the framework helper when the operand cannot be null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullOrEmptyStyleOnNotNullOperandUsesHelperAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string s)
                                           => string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, IsNullOrEmptyStyleSetting);
    }

    /// <summary>Verifies the is_null_or_empty style negates for <c>!=</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullOrEmptyStyleNegatesInequalityAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string s, bool flag)
                                      => flag && s {|PSH1204:!=|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string s, bool flag)
                                           => flag && !string.IsNullOrEmpty(s);
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, IsNullOrEmptyStyleSetting);
    }

    /// <summary>Verifies the is_null_or_empty style falls back to the pattern when the operand may be null, because the helper answers true for null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullOrEmptyStyleFallsBackToPatternWhenOperandMayBeNullAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string? s)
                                      => s {|PSH1204:!=|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string? s)
                                           => s is not { Length: 0 };
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, IsNullOrEmptyStyleSetting);
    }

    /// <summary>Verifies the project-wide key is honored when no rule-specific key is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralStyleKeyIsHonoredAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s.Length == 0;
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, "performancesharp.empty_string_style = length");
    }

    /// <summary>Verifies the rule-specific key overrides the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificStyleKeyWinsOverGeneralAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string s)
                                           => string.IsNullOrEmpty(s);
                                   }
                                   """;
        const string Settings = """
                                performancesharp.empty_string_style = length
                                performancesharp.PSH1204.empty_string_style = is_null_or_empty
                                """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, Settings);
    }

    /// <summary>Verifies an unrecognized style value falls back to the exact-equivalent pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrecognizedStyleFallsBackToPatternAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s is { Length: 0 };
                                   }
                                   """;
        await VerifyNet90WithConfigAsync(Source, FixedSource, "performancesharp.PSH1204.empty_string_style = nonsense");
    }

    /// <summary>Verifies the length style is offered below C# 9, where the pattern the default emits does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthStyleAppliesBeforeCSharp9Async()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public bool M(string s)
                                      => s {|PSH1204:==|} "";
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public bool M(string s)
                                           => s.Length == 0;
                                   }
                                   """;

        var test = new VerifyEmptyComparison.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
            FixedCode = FixedSource
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig(LengthStyleSetting)));
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp8));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification with one editorconfig setting applied.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="setting">The editorconfig lines to apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90WithConfigAsync(string source, string fixedSource, string setting)
    {
        var test = new VerifyEmptyComparison.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig(setting)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Builds an editorconfig file carrying one or more settings.</summary>
    /// <param name="setting">The editorconfig lines to apply.</param>
    /// <returns>The file content.</returns>
    private static string BuildConfig(string setting)
        => $"""
            root = true
            [*.cs]
            {setting}

            """;

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
