// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyInfiniteLoopStyle = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2267InfiniteLoopStyleAnalyzer,
    StyleSharp.Analyzers.Sst2267InfiniteLoopStyleCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2267 (normalize the infinite-loop style). The rule is disabled by default, so every
/// test enables it through an <c>.editorconfig</c> severity entry and, where relevant, sets the style option.
/// </summary>
public class InfiniteLoopStyleAnalyzerUnitTest
{
    /// <summary>Verifies a <c>for (;;)</c> loop is rewritten to <c>while (true)</c> under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeverForBecomesWhileUnderDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      {|SST2267:for|} (;;)
                                      {
                                          System.Console.WriteLine();
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           while (true)
                                           {
                                               System.Console.WriteLine();
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies a <c>while (true)</c> loop is rewritten to <c>for (;;)</c> when the style is <c>for</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeverWhileBecomesForWhenConfiguredAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      {|SST2267:while|} (true)
                                      {
                                          System.Console.WriteLine();
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           for (; ; )
                                           {
                                               System.Console.WriteLine();
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: "for");
    }

    /// <summary>Verifies a <c>while (true)</c> loop is left alone under the default <c>while</c> style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeverWhileIsCleanUnderDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      while (true)
                                      {
                                          System.Console.WriteLine();
                                      }
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies a <c>for (;;)</c> loop is left alone when the configured style is <c>for</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeverForIsCleanWhenForConfiguredAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      for (;;)
                                      {
                                          System.Console.WriteLine();
                                      }
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: "for");
    }

    /// <summary>Verifies a <c>for</c> loop that carries a condition is never a candidate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalForIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int count)
                                  {
                                      for (; count > 0; count--)
                                      {
                                          System.Console.WriteLine();
                                      }
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies a <c>while</c> loop with a non-<c>true</c> condition is never a candidate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalWhileIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool ready)
                                  {
                                      while (ready)
                                      {
                                          System.Console.WriteLine();
                                      }
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source, style: "for");
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled and the given style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="style">The <c>infinite_loop_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource, string? style)
    {
        var test = CreateTest(source, style);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <param name="style">The <c>infinite_loop_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? style)
    {
        var test = CreateTest(source, style);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2267 enabled and an optional style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="style">The <c>infinite_loop_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The configured test.</returns>
    private static VerifyInfiniteLoopStyle.Test CreateTest(string source, string? style)
    {
        var test = new VerifyInfiniteLoopStyle.Test
        {
            TestCode = source,
        };

        var config = "root = true\n\n[*.cs]\ndotnet_diagnostic.SST2267.severity = warning\n";
        if (style is not null)
        {
            config += $"stylesharp.infinite_loop_style = {style}\n";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
