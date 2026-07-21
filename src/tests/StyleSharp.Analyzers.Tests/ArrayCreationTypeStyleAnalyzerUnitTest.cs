// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyArrayCreationTypeStyle = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2270ArrayCreationTypeStyleAnalyzer,
    StyleSharp.Analyzers.Sst2270ArrayCreationTypeStyleCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2270 (normalize array-creation element-type style). The rule is disabled by default, so
/// every test enables it through an <c>.editorconfig</c> severity entry and, where relevant, sets the option.
/// </summary>
public class ArrayCreationTypeStyleAnalyzerUnitTest
{
    /// <summary>Verifies an implicit array gains its element type when the style is <c>explicit</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitBecomesExplicitWhenConfiguredAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int[] Make() => {|SST2270:new|}[] { 1, 2 };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int[] Make() => new int[] { 1, 2 };
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: "explicit");
    }

    /// <summary>Verifies an explicit array drops its element type when the style is <c>implicit</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitBecomesImplicitWhenConfiguredAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int[] Make() => new {|SST2270:int[]|} { 1, 2 };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int[] Make() => new[] { 1, 2 };
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: "implicit");
    }

    /// <summary>Verifies an obvious explicit array drops its element type under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObviousExplicitBecomesImplicitUnderDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int[] Make() => new {|SST2270:int[]|} { 1, 2 };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int[] Make() => new[] { 1, 2 };
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies a non-obvious explicit array keeps its element type under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonObviousExplicitIsCleanUnderDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private static int Value() => 1;

                                  public int[] Make() => new int[] { Value(), Value() };
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies an implicit array is left alone when the style is <c>implicit</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitIsCleanWhenImplicitConfiguredAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int[] Make() => new[] { 1, 2 };
                              }
                              """;
        await VerifyCleanAsync(Source, style: "implicit");
    }

    /// <summary>Verifies an array with an explicit size is never converted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SizedArrayIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int[] Make() => new int[2] { 1, 2 };
                              }
                              """;
        await VerifyCleanAsync(Source, style: "implicit");
    }

    /// <summary>Verifies an explicit array with no single best element type is never converted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBestTypeArrayIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public object[] Make() => new object[] { 1, "text" };
                              }
                              """;
        await VerifyCleanAsync(Source, style: "implicit");
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled and the given style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="style">The <c>array_creation_type_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource, string? style)
    {
        var test = CreateTest(source, style);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <param name="style">The <c>array_creation_type_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? style)
    {
        var test = CreateTest(source, style);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2270 enabled and an optional style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="style">The <c>array_creation_type_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The configured test.</returns>
    private static VerifyArrayCreationTypeStyle.Test CreateTest(string source, string? style)
    {
        var test = new VerifyArrayCreationTypeStyle.Test
        {
            TestCode = source,
        };

        var config = "root = true\n\n[*.cs]\ndotnet_diagnostic.SST2270.severity = warning\n";
        if (style is not null)
        {
            config += $"stylesharp.array_creation_type_style = {style}\n";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
