// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyAlmostExtension = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1709AlmostExtensionMethodAnalyzer>;
using VerifyAlmostExtensionFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1709AlmostExtensionMethodAnalyzer,
    StyleSharp.Analyzers.Sst1709AlmostExtensionMethodCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the almost-extension-method rule (SST1709, opt-in) and its extension-block fix.</summary>
public class AlmostExtensionMethodAnalyzerUnitTest
{
    /// <summary>Verifies a static helper in an Extensions class with no 'this' modifier is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingThisModifierReportedAsync()
        => await RunAnalyzerAsync(
            """
            public static class StringExtensions
            {
                public static bool {|SST1709:IsBlank|}(string text) => text.Length == 0;
            }
            """);

    /// <summary>Verifies a genuine 'this'-parameter extension method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenuineExtensionMethodIsCleanAsync()
        => await RunAnalyzerAsync(
            """
            public static class StringExtensions
            {
                public static bool IsBlank(this string text) => text.Length == 0;
            }
            """);

    /// <summary>Verifies a helper outside an Extensions-named class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonExtensionContainerIsCleanAsync()
        => await RunAnalyzerAsync(
            """
            public static class StringHelpers
            {
                public static bool IsBlank(string text) => text.Length == 0;
            }
            """);

    /// <summary>Verifies a private helper and a generic helper are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateAndGenericHelpersAreCleanAsync()
        => await RunAnalyzerAsync(
            """
            public static class StringExtensions
            {
                private static bool IsBlank(string text) => text.Length == 0;

                public static bool IsDefault<T>(T value) => value is null;
            }
            """);

    /// <summary>Verifies the fix converts the helper into an extension block member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConvertsToExtensionBlockAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static bool {|SST1709:IsBlank|}(string text) => text.Length == 0;
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
        var test = new VerifyAlmostExtensionFix.Test
        {
            TestCode = Source,
            FixedCode = FixedSource
        };
        AddPreview(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer verifier with the language version set to one that supports extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAnalyzerAsync(string source)
    {
        var test = new VerifyAlmostExtension.Test
        {
            TestCode = source
        };
        AddPreview(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Adds a solution transform that raises the language version to preview.</summary>
    /// <param name="transforms">The test's solution transforms.</param>
    private static void AddPreview(System.Collections.Generic.List<Func<Microsoft.CodeAnalysis.Solution, Microsoft.CodeAnalysis.ProjectId, Microsoft.CodeAnalysis.Solution>> transforms)
        => transforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
}
