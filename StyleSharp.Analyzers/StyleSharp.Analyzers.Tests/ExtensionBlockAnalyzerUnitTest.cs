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
            public static class Ext
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
            public static class Ext
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

    /// <summary>Verifies non-empty blocks with distinct receiver types are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DistinctNonEmptyBlocksAreCleanAsync()
        => await VerifyExtensionBlock.VerifyAnalyzerAsync(
            """
            public static class Ext
            {
                extension(string text)
                {
                    public bool IsEmpty => text.Length == 0;
                }

                extension(int value)
                {
                    public bool IsZero => value == 0;
                }
            }
            """);
}
