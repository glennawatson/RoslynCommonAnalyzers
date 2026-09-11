// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyExtensionBlock = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ExtensionBlockAnalyzer>;
using VerifyExtensionBlockFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExtensionBlockAnalyzer,
    StyleSharp.Analyzers.ExtensionContainerNamingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the C# 14 extension-block rules (SST1700/SST1701).</summary>
public class ExtensionBlockAnalyzerUnitTest
{
    /// <summary>The receiver-shape text an extension block declared over the string type classifies to.</summary>
    private const string StringReceiverShape = "string";

    /// <summary>Verifies an empty extension block is reported (SST1700).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyExtensionBlockReportedAsync() =>
        VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                {|SST1700:extension|}(string text)
                {
                }
            }
            """);

    /// <summary>Verifies a second extension block with the same receiver type is reported (SST1701).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DuplicateReceiverTypeReportedAsync() =>
        VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }

                {|SST1701:extension|}(string other)
                {
                    public int Size => other.Length;
                }
            }
            """);

    /// <summary>Verifies two blocks with the same receiver type but different generic constraints are not merged (SST1701).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameReceiverDifferentConstraintsIsCleanAsync() =>
        VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public interface IThing<T>
            {
            }

            public static class ThingExtensions
            {
                extension<T>(IThing<T> source)
                {
                    public bool IsPresent => source is not null;
                }

                extension<T>(IThing<T> source)
                    where T : struct
                {
                    public T Value => default;
                }
            }
            """);

    /// <summary>Verifies two blocks with the same receiver type and the same constraints are still reported (SST1701).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameReceiverSameConstraintsReportedAsync() =>
        VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public interface IThing<T>
            {
            }

            public static class ThingExtensions
            {
                extension<T>(IThing<T> source)
                    where T : struct
                {
                    public bool IsPresent => source is not null;
                }

                {|SST1701:extension|}<T>(IThing<T> source)
                    where T : struct
                {
                    public T Value => default;
                }
            }
            """);

    /// <summary>Verifies an extension block separated from the others by a member is reported (SST1702).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SeparatedExtensionBlockReportedAsync() =>
        VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                extension(int value)
                {
                    public bool IsZero => value == 0;
                }

                public static int Helper() => 0;

                {|SST1702:extension|}(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies an extension block on a broad receiver type is reported (SST1706).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BroadReceiverReportedAsync() =>
        VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public static class ObjectExtensions
            {
                {|SST1706:extension|}(object value)
                {
                    public bool IsNull => value is null;
                }
            }
            """);

    /// <summary>Verifies extension blocks out of receiver-type order are reported (SST1707, opt-in).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnorderedExtensionBlocksReportedAsync() =>
        VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }

                {|SST1707:extension|}(int value)
                {
                    public bool IsZero => value == 0;
                }
            }
            """);

    /// <summary>Verifies a container class not named with an 'Extensions' suffix is reported (SST1704).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ContainerWithoutExtensionsSuffixReportedAsync() =>
        RunAnalyzerAsync(
            """
            public static class {|SST1704:StringStuff|}
            {
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies a container class suffixed with <c>Mixins</c> is accepted by SST1704.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ContainerWithMixinsSuffixIsCleanAsync() =>
        RunAnalyzerAsync(
            """
            public static class StringMixins
            {
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies the code fix defaults to renaming the container with an <c>Extensions</c> suffix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainerNamingCodeFixDefaultsToExtensionsAsync()
    {
        const string Source = """
                              public static class {|SST1704:StringStuff|}
                              {
                                  extension(string text)
                                  {
                                      public bool IsEmpty => text.Length == 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public static class StringStuffExtensions
                                   {
                                       extension(string text)
                                       {
                                           public bool IsEmpty => text.Length == 0;
                                       }
                                   }
                                   """;

        await RunCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the code fix honors the rule-specific preferred-suffix configuration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainerNamingCodeFixHonorsRuleSpecificMixinsPreferenceAsync()
    {
        const string Source = """
                              public static class {|SST1704:StringStuff|}
                              {
                                  extension(string text)
                                  {
                                      public bool IsEmpty => text.Length == 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public static class StringStuffMixins
                                   {
                                       extension(string text)
                                       {
                                           public bool IsEmpty => text.Length == 0;
                                       }
                                   }
                                   """;
        const string EditorConfig = """
                                    root = true
                                    [*.cs]
                                    stylesharp.SST1704.preferred_suffix = Mixins
                                    """;

        await RunCodeFixAsync(Source, FixedSource, EditorConfig);
    }

    /// <summary>Verifies a classic extension method mixed with an extension block is reported (SST1705).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassicMethodMixedWithBlockReportedAsync() =>
        VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public static class TextExtensions
            {
                public static bool {|SST1705:IsBlank|}(this string text) => text.Length == 0;

                extension(string other)
                {
                    public int Size => other.Length;
                }
            }
            """);

    /// <summary>Verifies a class whose extensions are all classic methods draws no mixing report (SST1705).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// There is nothing to mix. Such a class is consistent as written, and moving it to the C# 14 syntax is
    /// the opt-in call SST1703 makes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassicMethodsWithoutABlockAreCleanAsync() =>
        RunAnalyzerAsync(
            """
            public static class TextExtensions
            {
                public static bool IsBlank(this string text) => text.Length == 0;

                public static int Size(this string text) => text.Length;
            }
            """);

    /// <summary>Verifies a classic extension method on <c>object</c> is reported (SST1706).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassicObjectReceiverReportedAsync() =>
        RunAnalyzerAsync(
            """
            public static class BroadExtensions
            {
                public static string Describe(this {|SST1706:object|} value) => value.ToString();
            }
            """);

    /// <summary>Verifies a classic extension method on an unconstrained type parameter is reported (SST1706).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassicUnconstrainedTypeParameterReportedAsync() =>
        RunAnalyzerAsync(
            """
            public static class IdentityExtensions
            {
                public static T Identity<T>(this {|SST1706:T|} value) => value;
            }
            """);

    /// <summary>Verifies a classic extension method on a constrained type parameter draws no broad-receiver report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassicConstrainedTypeParameterDrawsNoBroadReceiverAsync() =>
        RunAnalyzerAsync(
            """
            using System;

            public static class ComparableExtensions
            {
                public static int RankOf<T>(this T value)
                    where T : IComparable<T> => value.CompareTo(value);
            }
            """);

    /// <summary>Verifies a classic extension method on a specific type draws no broad-receiver report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassicSpecificReceiverDrawsNoBroadReceiverAsync() =>
        RunAnalyzerAsync(
            """
            public static class TextExtensions
            {
                public static bool IsBlank(this string text) => text.Length == 0;
            }
            """);

    /// <summary>Verifies non-empty blocks with distinct receiver types are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DistinctNonEmptyBlocksAreCleanAsync() =>
        RunAnalyzerAsync(
            """
            public static class SampleExtensions
            {
                extension(int value)
                {
                    public bool IsZero => value == 0;
                }

                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies simple receiver types can be classified without allocating receiver text.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverShapeFastPathClassifiesPredefinedTypes()
    {
        var receiverType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword));

        await Assert.That(ExtensionBlockHelper.TryClassifyReceiverShape(receiverType, out var shape)).IsTrue();
        await Assert.That(shape).IsEqualTo(StringReceiverShape);
    }

    /// <summary>Verifies unsupported receiver shapes fall back to the slower receiver-text path.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverShapeFastPathFallsBackForQualifiedNames()
    {
        var receiverType = SyntaxFactory.QualifiedName(
            SyntaxFactory.IdentifierName("System"),
            SyntaxFactory.IdentifierName(nameof(String)));

        await Assert.That(ExtensionBlockHelper.TryClassifyReceiverShape(receiverType, out var shape)).IsFalse();
        await Assert.That(shape).IsNull();
    }

    /// <summary>Verifies simple broad receivers are classified without extra string comparisons in the analyzer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverClassificationFastPathMarksBroadObjectAsync()
    {
        var receiverType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));

        await Assert.That(ExtensionBlockHelper.TryClassifyReceiver(receiverType, out var shape, out var isBroadReceiver)).IsTrue();
        await Assert.That(shape).IsEqualTo("object");
        await Assert.That(isBroadReceiver).IsTrue();
    }

    /// <summary>Verifies simple non-broad receivers stay on the cheap classified path.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverClassificationFastPathKeepsNonBroadStringAsync()
    {
        var receiverType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword));

        await Assert.That(ExtensionBlockHelper.TryClassifyReceiver(receiverType, out var shape, out var isBroadReceiver)).IsTrue();
        await Assert.That(shape).IsEqualTo(StringReceiverShape);
        await Assert.That(isBroadReceiver).IsFalse();
    }

    /// <summary>Verifies receiver ordering skips the first block and reports only descending lexical order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverOrderHelperClassifiesDescendingOnlyAsync()
    {
        await Assert.That(ExtensionBlockAnalyzer.IsOutOfOrderReceiver(StringReceiverShape, null)).IsFalse();
        await Assert.That(ExtensionBlockAnalyzer.IsOutOfOrderReceiver(StringReceiverShape, "int")).IsFalse();
        await Assert.That(ExtensionBlockAnalyzer.IsOutOfOrderReceiver("int", StringReceiverShape)).IsTrue();
    }

    /// <summary>Verifies duplicate detection only reports equal immediate receivers.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateReceiverHelperMatchesOrdinalEqualityAsync()
    {
        await Assert.That(ExtensionBlockAnalyzer.IsDuplicateImmediateReceiver(StringReceiverShape, StringReceiverShape)).IsTrue();
        await Assert.That(ExtensionBlockAnalyzer.IsDuplicateImmediateReceiver(StringReceiverShape, "int")).IsFalse();
    }

    /// <summary>Runs the analyzer verifier with the language version set to one that supports extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAnalyzerAsync(string source)
    {
        var test = new VerifyExtensionBlock.Test { TestCode = source };

        ApplyExtensionBlockParseOptions(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the code-fix verifier with the language version set to one that supports extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="fixedSource">The expected fixed code.</param>
    /// <param name="editorConfig">Optional analyzer-config content.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunCodeFixAsync(string source, string fixedSource, string? editorConfig = null)
    {
        var test = new VerifyExtensionBlockFix.Test { TestCode = source, FixedCode = fixedSource };

        if (editorConfig is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", editorConfig));
        }

        ApplyExtensionBlockParseOptions(test.SolutionTransforms);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Applies preview parse options to a verifier so extension blocks parse.</summary>
    /// <param name="solutionTransforms">The solution-transform collection to update.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyExtensionBlockParseOptions(List<Func<Solution, ProjectId, Solution>> solutionTransforms) => solutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
}
