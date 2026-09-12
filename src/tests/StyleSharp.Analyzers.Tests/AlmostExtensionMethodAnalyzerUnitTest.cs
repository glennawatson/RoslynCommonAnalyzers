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
                                       /// <summary>Extension members for <c>string</c>.</summary>
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
                                       /// <summary>Extension members for <c>string</c>.</summary>
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

    /// <summary>Verifies every reported helper in one class is converted, not just the first.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Each conversion replaces the whole containing class, so two reports in the same class describe edits
    /// to the same node. A fix-all that resolves only one of them leaves the rest reported.
    /// </remarks>
    [Test]
    public async Task ConvertsEveryHelperInTheClassAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static bool {|SST1709:IsBlank|}(string text) => text.Trim().Length == 0;

                                  public static bool {|SST1709:IsEmpty|}(string text) => text.Length == 0;
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       /// <summary>Extension members for <c>string</c>.</summary>
                                       extension(string text)
                                       {
                                           public bool IsBlank() => text.Trim().Length == 0;

                                           public bool IsEmpty() => text.Length == 0;
                                       }
                                   }
                                   """;
        var test = new VerifyAlmostExtensionFix.Test { TestCode = Source, FixedCode = FixedSource };
        AddPreview(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a helper whose first parameter is a delegate is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A receiver of delegate type reads as an extension on every method group in the program, which is
    /// never what the helper meant. The conversion has no sensible result, so the shape stays silent.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelegateTypedFirstParameterIsCleanAsync() =>
        RunAnalyzerAsync(
            """
            public static class BuilderExtensions
            {
                internal static void Configure(System.Action<string> configure) => configure("value");
            }
            """);

    /// <summary>Verifies a helper whose nullable receiver is null-guarded is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A nullable receiver is legal on a block, so the guard is preserved by the conversion and the report
    /// stays actionable. Only a receiver the conversion cannot express is skipped.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullableReceiverGuardedAsNoOpIsReportedAsync() =>
        RunAnalyzerAsync(
            """
            #nullable enable
            public static class ResolverExtensions
            {
                internal static void {|SST1709:Register|}(string? resolver, int count)
                {
                    if (resolver is null)
                    {
                        return;
                    }

                    _ = resolver.Length + count;
                }
            }
            """);

    /// <summary>Verifies the block the fix opens is documented.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>An extension block without a summary is what SST1654 reports, so the fix must not write one.</remarks>
    [Test]
    public async Task GeneratedBlockCarriesASummaryAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  /// <summary>Reports whether the text is blank.</summary>
                                  /// <param name="text">The text to test.</param>
                                  /// <returns><see langword="true"/> when blank.</returns>
                                  internal static bool {|SST1709:IsBlank|}(string text) => text.Trim().Length == 0;
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       /// <summary>Extension members for <c>string</c>.</summary>
                                       extension(string text)
                                       {
                                           /// <summary>Reports whether the text is blank.</summary>
                                           /// <returns><see langword="true"/> when blank.</returns>
                                           internal bool IsBlank() => text.Trim().Length == 0;
                                       }
                                   }
                                   """;
        var test = new VerifyAlmostExtensionFix.Test { TestCode = Source, FixedCode = FixedSource };
        AddPreview(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a directive around an unrelated member does not block the conversion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The reported method and the block it joins both sit outside the directive, so moving one into the
    /// other does not split a conditional region. Declining here leaves the report with no fix available.
    /// </remarks>
    [Test]
    public async Task ConvertsWhenADirectiveWrapsAnUnrelatedMemberAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  extension(string text)
                                  {
                                      public bool IsEmpty() => text.Length == 0;
                                  }

                                  public static bool {|SST1709:IsBlank|}(string text) => text.Trim().Length == 0;

                              #if NET
                                  private static int Width(string text) => text.Length;
                              #endif
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

                                   #if NET
                                       private static int Width(string text) => text.Length;
                                   #endif
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
