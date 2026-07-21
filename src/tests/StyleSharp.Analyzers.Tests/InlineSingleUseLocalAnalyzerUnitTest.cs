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

    /// <summary>Runs a code-fix verification with the disabled rule enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource)
    {
        var test = CreateTest(source);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source)
    {
        var test = CreateTest(source);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2266 enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyInlineSingleUseLocal.Test CreateTest(string source)
    {
        var test = new VerifyInlineSingleUseLocal.Test
        {
            TestCode = source,
        };

        const string Config = """
                              root = true

                              [*.cs]
                              dotnet_diagnostic.SST2266.severity = warning
                              """;
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        return test;
    }
}
