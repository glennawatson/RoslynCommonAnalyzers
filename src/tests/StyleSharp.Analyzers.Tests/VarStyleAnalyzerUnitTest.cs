// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
        await RunAsync(Source, FixedSource, style: "always");
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
        await RunAsync(Source, FixedSource, style: "never");
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
        await VerifyCleanAsync(Source, style: "always");
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
        await VerifyCleanAsync(Source, style: "always");
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
        await RunAsync(Source, FixedSource, style: "always");
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
        await RunAsync(Source, FixedSource, style: "never");
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
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? style)
    {
        var test = CreateTest(source, style);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2271 enabled and an optional style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="style">The <c>use_var</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The configured test.</returns>
    private static VerifyVarStyle.Test CreateTest(string source, string? style)
    {
        var test = new VerifyVarStyle.Test
        {
            TestCode = source,
        };

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
