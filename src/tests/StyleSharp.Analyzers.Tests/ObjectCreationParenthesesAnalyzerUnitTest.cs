// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyObjectCreationParentheses = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2268ObjectCreationParenthesesAnalyzer,
    StyleSharp.Analyzers.Sst2268ObjectCreationParenthesesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2268 (normalize object-creation parentheses). The rule is disabled by default, so every
/// test enables it through an <c>.editorconfig</c> severity entry and, where relevant, sets the style option.
/// </summary>
public class ObjectCreationParenthesesAnalyzerUnitTest
{
    /// <summary>Verifies empty parentheses before an initializer are dropped under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyParenthesesAreDroppedUnderDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int X { get; set; }

                                  public C Make() => new {|SST2268:C|}() { X = 1 };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int X { get; set; }

                                       public C Make() => new C { X = 1 };
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies empty parentheses are added before an initializer when the style is <c>include</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyParenthesesAreAddedWhenIncludeAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int X { get; set; }

                                  public C Make() => new {|SST2268:C|} { X = 1 };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int X { get; set; }

                                       public C Make() => new C() { X = 1 };
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: "include");
    }

    /// <summary>Verifies a collection initializer's empty parentheses are dropped under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionInitializerParenthesesAreDroppedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public List<int> Make() => new {|SST2268:List<int>|}() { 1, 2 };
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public List<int> Make() => new List<int> { 1, 2 };
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies a creation already in the default style is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OmittedFormIsCleanUnderDefaultAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int X { get; set; }

                                  public C Make() => new C { X = 1 };
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies a creation with real constructor arguments is never touched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorArgumentsAreCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public C(int count)
                                  {
                                  }

                                  public int X { get; set; }

                                  public C Make() => new C(4) { X = 1 };
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies a creation with no initializer keeps its parentheses under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoInitializerIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public C Make() => new C();
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled and the given style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="style">The <c>object_creation_parentheses</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource, string? style)
    {
        var test = CreateTest(source, style);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <param name="style">The <c>object_creation_parentheses</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? style)
    {
        var test = CreateTest(source, style);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2268 enabled and an optional style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="style">The <c>object_creation_parentheses</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The configured test.</returns>
    private static VerifyObjectCreationParentheses.Test CreateTest(string source, string? style)
    {
        var test = new VerifyObjectCreationParentheses.Test
        {
            TestCode = source,
        };

        var config = "root = true\n\n[*.cs]\ndotnet_diagnostic.SST2268.severity = warning\n";
        if (style is not null)
        {
            config += $"stylesharp.object_creation_parentheses = {style}\n";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
