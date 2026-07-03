// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyArrayEmpty = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1001UseArrayEmptyAnalyzer,
    PerformanceSharp.Analyzers.Psh1001UseArrayEmptyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1001 (avoid allocating zero-length arrays) and its code fix.</summary>
public class UseArrayEmptyAnalyzerUnitTest
{
    /// <summary>Verifies a literal zero-length allocation in a target-typed spot becomes a collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroLengthArrayReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] M() => {|PSH1001:new int[0]|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] M() => [];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an omitted-size creation with an empty initializer is reported and replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyInitializerArrayReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string[] M() => {|PSH1001:new string[] { }|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string[] M() => [];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a literal zero size combined with an empty initializer is reported and replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroLengthWithEmptyInitializerReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] M() => {|PSH1001:new int[0] { }|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] M() => [];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a zero-length jagged creation in a matching arrow body becomes a collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JaggedZeroLengthArrayReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[][] M() => {|PSH1001:new int[0][]|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[][] M() => [];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Fix All replaces every zero-length allocation in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] M1() => {|PSH1001:new int[0]|};

                                  public string[] M2() => {|PSH1001:new string[] { }|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] M1() => [];

                                       public string[] M2() => [];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an explicitly typed local initializer becomes a collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypedLocalUsesCollectionExpressionAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M()
                                  {
                                      int[] values = {|PSH1001:new int[0]|};
                                      return values.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M()
                                       {
                                           int[] values = [];
                                           return values.Length;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a var-declared local keeps Array.Empty because [] has no target type there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VarLocalFallsBackToArrayEmptyAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M()
                                  {
                                      var values = {|PSH1001:new int[0]|};
                                      return values.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M()
                                       {
                                           var values = System.Array.Empty<int>();
                                           return values.Length;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a field initializer becomes a collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldInitializerUsesCollectionExpressionAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] Values = {|PSH1001:new int[0]|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] Values = [];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a property initializer becomes a collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyInitializerUsesCollectionExpressionAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] Values { get; } = {|PSH1001:new int[0]|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] Values { get; } = [];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a return statement in a block body becomes a collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnStatementUsesCollectionExpressionAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string[] M()
                                  {
                                      return {|PSH1001:new string[0]|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string[] M()
                                       {
                                           return [];
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an assignment to a matching array target becomes a collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignmentToArrayFieldUsesCollectionExpressionAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int[] _values = {|PSH1001:new int[0]|};

                                  public int M()
                                  {
                                      _values = {|PSH1001:new int[0]|};
                                      return _values.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int[] _values = [];

                                       public int M()
                                       {
                                           _values = [];
                                           return _values.Length;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a covariant assignment keeps Array.Empty so the instance's element type is preserved.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CovariantAssignmentFallsBackToArrayEmptyAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M()
                                  {
                                      object[] values = {|PSH1001:new string[0]|};
                                      return values.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M()
                                       {
                                           object[] values = System.Array.Empty<string>();
                                           return values.Length;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an argument position keeps Array.Empty to avoid changing overload resolution.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentFallsBackToArrayEmptyAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M() => Sum({|PSH1001:new int[0]|});

                                  private static int Sum(int[] values) => values.Length;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M() => Sum(System.Array.Empty<int>());

                                       private static int Sum(int[] values) => values.Length;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies switching the option off keeps Array.Empty even in target-typed spots.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionOffFallsBackToArrayEmptyAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] M() => {|PSH1001:new int[0]|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] M() => System.Array.Empty<int>();
                                   }
                                   """;
        var test = CreateNet90Test(Source, FixedSource);
        const string Config = """
                              root = true

                              [*.cs]
                              performancesharp.prefer_collection_expressions = false
                              """;
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies pre-C# 12 code keeps Array.Empty because collection expressions do not exist there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CSharp11FallsBackToArrayEmptyAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] M() => {|PSH1001:new int[0]|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] M() => System.Array.Empty<int>();
                                   }
                                   """;
        var test = CreateNet90Test(Source, FixedSource);
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp11));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a non-zero constant length is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonZeroLengthArrayIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public int[] M() => new int[1];
            }
            """);

    /// <summary>Verifies a variable length is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VariableLengthArrayIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public int[] M(int n) => new int[n];
            }
            """);

    /// <summary>Verifies a zero-length creation inside an attribute argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeArgumentArrayIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class SomeAttribute : System.Attribute
            {
                public SomeAttribute(int[] values)
                {
                }
            }

            [Some(new int[0])]
            public class C
            {
            }
            """);

    /// <summary>Verifies a multi-dimensional zero-length creation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiDimensionalZeroLengthArrayIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public int[,] M() => new int[0, 0];
            }
            """);

    /// <summary>Creates a code-fix verifier test against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyArrayEmpty.Test CreateNet90Test(string source, string fixedSource)
        => new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
        => await CreateNet90Test(source, fixedSource).RunAsync(CancellationToken.None);

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90CleanAsync(string source)
        => await VerifyNet90Async(source, source);
}
