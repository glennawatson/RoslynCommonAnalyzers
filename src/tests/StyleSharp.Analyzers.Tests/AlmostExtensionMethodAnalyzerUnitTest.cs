// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MissingThisModifierReportedAsync() =>
        RunAnalyzerAsync(
            """
            public static class StringExtensions
            {
                public static bool {|SST1709:IsBlank|}(string text) => text.Length == 0;
            }
            """);

    /// <summary>Verifies a genuine 'this'-parameter extension method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GenuineExtensionMethodIsCleanAsync() =>
        RunAnalyzerAsync(
            """
            public static class StringExtensions
            {
                public static bool IsBlank(this string text) => text.Length == 0;
            }
            """);

    /// <summary>Verifies a helper outside an Extensions-named class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonExtensionContainerIsCleanAsync() =>
        RunAnalyzerAsync(
            """
            public static class StringHelpers
            {
                public static bool IsBlank(string text) => text.Length == 0;
            }
            """);

    /// <summary>Verifies a private helper and a generic helper are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateAndGenericHelpersAreCleanAsync() =>
        RunAnalyzerAsync(
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
        var test = new VerifyAlmostExtensionFix.Test { TestCode = Source, FixedCode = FixedSource };
        AddPreview(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the documentation moves with the method and loses the receiver's parameter tag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The converted method keeps its documentation, and the parameter that became the block receiver is no
    /// longer one of its parameters — so a <c>param</c> tag naming it would document nothing.
    /// </remarks>
    [Test]
    public async Task KeepsDocumentationAndDropsTheReceiverTagAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  /// <summary>Reports whether the text is blank.</summary>
                                  /// <param name="text">The text to test.</param>
                                  /// <param name="trim">Whether to trim first.</param>
                                  /// <returns><see langword="true"/> when blank.</returns>
                                  public static bool {|SST1709:IsBlank|}(string text, bool trim) => trim ? text.Trim().Length == 0 : text.Length == 0;
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       extension(string text)
                                       {
                                           /// <summary>Reports whether the text is blank.</summary>
                                           /// <param name="trim">Whether to trim first.</param>
                                           /// <returns><see langword="true"/> when blank.</returns>
                                           public bool IsBlank(bool trim) => trim ? text.Trim().Length == 0 : text.Length == 0;
                                       }
                                   }
                                   """;
        var test = new VerifyAlmostExtensionFix.Test { TestCode = Source, FixedCode = FixedSource };
        AddPreview(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the member joins the block that already declares the same receiver.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Opening a second block for a receiver the class already extends is what SST1701 reports.</remarks>
    [Test]
    public async Task JoinsTheExistingBlockForTheSameReceiverAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  extension(string text)
                                  {
                                      public bool IsEmpty() => text.Length == 0;
                                  }

                                  public static bool {|SST1709:IsBlank|}(string text) => text.Trim().Length == 0;
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       extension(string text)
                                       {
                                           public bool IsEmpty() => text.Length == 0;

                                           public bool IsBlank() => text.Trim().Length == 0;
                                       }
                                   }
                                   """;
        var test = new VerifyAlmostExtensionFix.Test { TestCode = Source, FixedCode = FixedSource };
        AddPreview(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer verifier with the language version set to one that supports extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAnalyzerAsync(string source)
    {
        var test = new VerifyAlmostExtension.Test { TestCode = source };
        AddPreview(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Adds a solution transform that raises the language version to preview.</summary>
    /// <param name="transforms">The test's solution transforms.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddPreview(List<Func<Microsoft.CodeAnalysis.Solution, Microsoft.CodeAnalysis.ProjectId, Microsoft.CodeAnalysis.Solution>> transforms) =>
        transforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
}
