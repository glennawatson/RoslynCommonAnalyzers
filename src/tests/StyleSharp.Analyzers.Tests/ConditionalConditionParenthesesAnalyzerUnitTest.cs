// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConditionalConditionParentheses = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2269ConditionalConditionParenthesesAnalyzer,
    StyleSharp.Analyzers.Sst2269ConditionalConditionParenthesesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2269 (normalize conditional-condition parentheses). The rule is disabled by default, so
/// every test enables it through an <c>.editorconfig</c> severity entry and, where relevant, sets the option.
/// </summary>
public class ConditionalConditionParenthesesAnalyzerUnitTest
{
    /// <summary>Verifies parentheses around a single identifier condition are dropped under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleIdentifierParenthesesAreDroppedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool ready) => {|SST2269:(ready)|} ? 1 : 2;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(bool ready) => ready ? 1 : 2;
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies parentheses around a literal condition are dropped under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralConditionParenthesesAreDroppedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M() => {|SST2269:(true)|} ? 1 : 2;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M() => true ? 1 : 2;
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies an unparenthesized condition is wrapped when the style is <c>include</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionIsWrappedWhenIncludeAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool ready) => {|SST2269:ready|} ? 1 : 2;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(bool ready) => (ready) ? 1 : 2;
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: "include");
    }

    /// <summary>Verifies a parenthesized larger condition keeps its parentheses under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedLargerConditionIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool a, bool b) => (a && b) ? 1 : 2;
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies an unparenthesized single token is left alone under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnparenthesizedConditionIsCleanUnderDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool ready) => ready ? 1 : 2;
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies an already parenthesized condition is left alone when the style is <c>include</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedConditionIsCleanWhenIncludeAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool ready) => (ready) ? 1 : 2;
                              }
                              """;
        await VerifyCleanAsync(Source, style: "include");
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled and the given style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="style">The <c>conditional_condition_parentheses</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource, string? style)
    {
        var test = CreateTest(source, style);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <param name="style">The <c>conditional_condition_parentheses</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? style)
    {
        var test = CreateTest(source, style);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2269 enabled and an optional style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="style">The <c>conditional_condition_parentheses</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The configured test.</returns>
    private static VerifyConditionalConditionParentheses.Test CreateTest(string source, string? style)
    {
        var test = new VerifyConditionalConditionParentheses.Test
        {
            TestCode = source,
        };

        var config = "root = true\n\n[*.cs]\ndotnet_diagnostic.SST2269.severity = warning\n";
        if (style is not null)
        {
            config += $"stylesharp.conditional_condition_parentheses = {style}\n";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
