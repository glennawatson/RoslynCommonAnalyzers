// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUnusedReceiver = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1708UnusedExtensionReceiverAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the unused-extension-receiver rule (SST1708).</summary>
public class UnusedExtensionReceiverAnalyzerUnitTest
{
    /// <summary>Verifies an extension method that never reads its receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverNeverReadReportedAsync()
        => await VerifyUnusedReceiver.VerifyAnalyzerAsync(
            """
            public static class Ext
            {
                public static int Zero(this string {|SST1708:text|}) => 0;
            }
            """);

    /// <summary>Verifies an extension method that reads its receiver is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverReadIsCleanAsync()
        => await VerifyUnusedReceiver.VerifyAnalyzerAsync(
            """
            public static class Ext
            {
                public static int Length(this string text) => text.Length;
            }
            """);

    /// <summary>Verifies a block-bodied extension that reads the receiver is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockBodyReceiverReadIsCleanAsync()
        => await VerifyUnusedReceiver.VerifyAnalyzerAsync(
            """
            public static class Ext
            {
                public static bool IsEmpty(this string text)
                {
                    return text.Length == 0;
                }
            }
            """);

    /// <summary>Verifies a plain (non-extension) static method is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonExtensionMethodIsCleanAsync()
        => await VerifyUnusedReceiver.VerifyAnalyzerAsync(
            """
            public static class Ext
            {
                public static int Zero(string text) => 0;
            }
            """);

    /// <summary>Verifies a discard-named receiver is exempt.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardReceiverIsCleanAsync()
        => await VerifyUnusedReceiver.VerifyAnalyzerAsync(
            """
            public static class Ext
            {
                public static int Zero(this string _) => 0;
            }
            """);
}
