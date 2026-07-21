// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFoldFluentCallChain = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2265FoldFluentCallChainAnalyzer,
    StyleSharp.Analyzers.Sst2265FoldFluentCallChainCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2265 (fold consecutive fluent calls into one chain). The rule is disabled by default, so
/// every test enables it through an <c>.editorconfig</c> severity entry.
/// </summary>
public class FoldFluentCallChainAnalyzerUnitTest
{
    /// <summary>Verifies two consecutive fluent calls on one receiver fold into a chain.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoCallsFoldAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  public void M(StringBuilder builder)
                                  {
                                      {|SST2265:builder|}.Append("a");
                                      builder.Append("b");
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public sealed class C
                                   {
                                       public void M(StringBuilder builder)
                                       {
                                           builder.Append("a").Append("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies three consecutive fluent calls fold into a single chain.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeCallsFoldAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  public void M(StringBuilder builder)
                                  {
                                      {|SST2265:builder|}.Append("a");
                                      builder.Append("b");
                                      builder.Append("c");
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public sealed class C
                                   {
                                       public void M(StringBuilder builder)
                                       {
                                           builder.Append("a").Append("b").Append("c");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies calls on different receivers are not folded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentReceiversAreCleanAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  public void M(StringBuilder first, StringBuilder second)
                                  {
                                      first.Append("a");
                                      second.Append("b");
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies calls that do not return the receiver's type are not folded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonFluentCallsAreCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public void M(List<int> items)
                                  {
                                      items.Add(1);
                                      items.Add(2);
                                  }
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies calls on a non-trivial receiver expression are not folded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImpureReceiverIsCleanAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      Get().Append("a");
                                      Get().Append("b");
                                  }

                                  private static StringBuilder Get() => new StringBuilder();
                              }
                              """;
        await VerifyCleanAsync(Source);
    }

    /// <summary>Verifies a single fluent call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleCallIsCleanAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  public void M(StringBuilder builder) => builder.Append("a");
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

    /// <summary>Creates a verifier test with SST2265 enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyFoldFluentCallChain.Test CreateTest(string source)
    {
        var test = new VerifyFoldFluentCallChain.Test
        {
            TestCode = source,
        };

        const string Config = """
                              root = true

                              [*.cs]
                              dotnet_diagnostic.SST2265.severity = warning
                              """;
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        return test;
    }
}
