// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using VerifyMixedStylesFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExtensionBlockAnalyzer,
    StyleSharp.Analyzers.ExtensionBlockMemberCodeFixProvider>;
using VerifyPreferBlockFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1703PreferExtensionBlockAnalyzer,
    StyleSharp.Analyzers.ExtensionBlockMemberCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="ExtensionBlockMemberCodeFixProvider"/> (SST1703, SST1705).</summary>
public class ExtensionBlockMemberCodeFixProviderUnitTest
{
    /// <summary>Verifies a classic extension method becomes a new extension block (SST1703).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassicExtensionMethodMovesIntoANewBlockAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static bool {|SST1703:IsBlank|}(this string text) => text.Length == 0;
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       extension(string text)
                                       {
                                           public bool IsBlank() => text.Length == 0;
                                       }
                                   }
                                   """;
        await RunPreferBlockAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method mixed in beside a block joins that block when the receiver matches (SST1705).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedMethodJoinsTheMatchingBlockAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static bool {|SST1705:IsBlank|}(this string text) => text.Length == 0;

                                  extension(string text)
                                  {
                                      public int Words => text.Split(' ').Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       extension(string text)
                                       {
                                           public int Words => text.Split(' ').Length;
                                           public bool IsBlank() => text.Length == 0;
                                       }
                                   }
                                   """;
        await RunMixedStylesAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method whose receiver name differs from the block's gets its own block, so the body still binds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodWithADifferentReceiverNameGetsItsOwnBlockAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static bool {|SST1705:IsBlank|}(this string value) => value.Length == 0;

                                  extension(string text)
                                  {
                                      public int Words => text.Split(' ').Length;
                                  }
                              }
                              """;

        // Two blocks over the same receiver type then draw SST1701, which asks for them to be combined —
        // that merge has to rename one receiver, so it stays a separate decision from this fix.
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       extension(string value)
                                       {
                                           public bool IsBlank() => value.Length == 0;
                                       }

                                       {|SST1701:extension|}(string text)
                                       {
                                           public int Words => text.Split(' ').Length;
                                       }
                                   }
                                   """;
        await RunMixedStylesAsync(Source, FixedSource);
    }

    /// <summary>Verifies a generic extension method is reported but not fixed, since its type parameters need a decision.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericExtensionMethodIsNotFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public static class StringExtensions
                              {
                                  public static bool {|SST1703:IsEmpty|}<T>(this IReadOnlyCollection<T> items) => items.Count == 0;
                              }
                              """;

        // The diagnostic survives, because no fix is offered for it — the source is expected to be untouched.
        await RunPreferBlockAsync(Source, Source);
    }

    /// <summary>Runs the SST1703 verifier at a language version that has extension blocks.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunPreferBlockAsync(string source, string fixedSource)
    {
        var test = new VerifyPreferBlockFix.Test
        {
            TestCode = source,
            FixedCode = fixedSource
        };

        AddPreviewLanguageVersion(test);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the SST1705 verifier at a language version that has extension blocks.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunMixedStylesAsync(string source, string fixedSource)
    {
        var test = new VerifyMixedStylesFix.Test
        {
            TestCode = source,
            FixedCode = fixedSource
        };

        AddPreviewLanguageVersion(test);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Parses the test project at the language version that supports extension blocks.</summary>
    /// <param name="test">The test to configure.</param>
    private static void AddPreviewLanguageVersion(Microsoft.CodeAnalysis.Testing.AnalyzerTest<Microsoft.CodeAnalysis.Testing.DefaultVerifier> test)
        => test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
}
