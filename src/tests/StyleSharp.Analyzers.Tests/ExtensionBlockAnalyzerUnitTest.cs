// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyExtensionBlock = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ExtensionBlockAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the C# 14 extension-block rules (SST1700/SST1701).</summary>
public class ExtensionBlockAnalyzerUnitTest
{
    /// <summary>Verifies an empty extension block is reported (SST1700).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyExtensionBlockReportedAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
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
    [Test]
    public async Task DuplicateReceiverTypeReportedAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
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

    /// <summary>Verifies an extension block separated from the others by a member is reported (SST1702).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatedExtensionBlockReportedAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
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
    [Test]
    public async Task BroadReceiverReportedAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
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
    [Test]
    public async Task UnorderedExtensionBlocksReportedAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
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
    [Test]
    public async Task ContainerWithoutExtensionsSuffixReportedAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public static class {|SST1704:StringStuff|}
            {
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies a classic extension method mixed with an extension block is reported (SST1705).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassicMethodMixedWithBlockReportedAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
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

    /// <summary>Verifies non-empty blocks with distinct receiver types are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DistinctNonEmptyBlocksAreCleanAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
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
}
