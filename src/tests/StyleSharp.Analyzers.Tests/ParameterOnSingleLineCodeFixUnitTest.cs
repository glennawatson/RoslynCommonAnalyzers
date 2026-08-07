// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySingleLine = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ParameterListLayoutAnalyzer,
    StyleSharp.Analyzers.Sst1118ParameterOnSingleLineCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for the SST1118 code fix (put a wrapped parameter or argument on one line). The rule is
/// disabled by default, so every test enables it through an <c>.editorconfig</c> severity entry.
/// </summary>
public class ParameterOnSingleLineCodeFixUnitTest
{
    /// <summary>Verifies a wrapped argument is collapsed onto one line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrappedArgumentIsCollapsedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M() => Add({|SST1118:1 +
                                      2|});

                                  private static int Add(int value) => value;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M() => Add(1 + 2);

                                       private static int Add(int value) => value;
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a wrapped attribute argument is collapsed onto one line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrappedAttributeArgumentIsCollapsedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  [System.Obsolete({|SST1118:"first" +
                                      "second"|})]
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       [System.Obsolete("first" + "second")]
                                       public void M()
                                       {
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment inside the wrapped item leaves it alone rather than losing the comment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ItemHoldingACommentIsNotCollapsedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M() => Add({|SST1118:1 +
                                      // the offset
                                      2|});

                                  private static int Add(int value) => value;
                              }
                              """;
        await VerifyNoFixAsync(Source);
    }

    /// <summary>Verifies an item that would not fit the configured line length is left wrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Collapsing here would only trade SST1118 for SST1521. Extracting the item into a local is the way
    /// out, and choosing that is the author's call, so no fix is offered.
    /// </remarks>
    [Test]
    public async Task ItemThatWouldOverflowTheLineIsNotCollapsedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int aLongParameterName, int anotherLongParameterName) => Add({|SST1118:aLongParameterName +
                                      anotherLongParameterName|});

                                  private static int Add(int value) => value;
                              }
                              """;
        await VerifyNoFixAsync(Source, "stylesharp.max_line_length = 60");
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource)
    {
        var test = CreateTest(source, optionLine: null);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification where the diagnostic is reported but no fix is offered.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="optionLine">An optional extra <c>.editorconfig</c> option line.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNoFixAsync(string source, string? optionLine = null)
    {
        var test = CreateTest(source, optionLine);
        test.FixedCode = source;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST1118 enabled and any extra option applied.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="optionLine">An optional extra <c>.editorconfig</c> option line.</param>
    /// <returns>The configured test.</returns>
    private static VerifySingleLine.Test CreateTest(string source, string? optionLine)
    {
        var test = new VerifySingleLine.Test
        {
            TestCode = source,
        };

        var config = "root = true\n\n[*.cs]\ndotnet_diagnostic.SST1118.severity = warning\n";
        if (optionLine is not null)
        {
            config += optionLine + "\n";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
