// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDisplay = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2334MissingDebuggerDisplayAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2334 (give a public type a debugger-display attribute).</summary>
public class Sst2334MissingDebuggerDisplayAnalyzerUnitTest
{
    /// <summary>Verifies a public class with no debugger-display attribute is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicClassWithoutAttributeIsReportedAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public class {|SST2334:Money|}
            {
                public int Amount { get; set; }
            }
            """);

    /// <summary>Verifies a public struct with no debugger-display attribute is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicStructWithoutAttributeIsReportedAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public struct {|SST2334:Point|}
            {
                public int X { get; set; }
            }
            """);

    /// <summary>Verifies a type that already carries the attribute is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TypeWithAttributeIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            [DebuggerDisplay("{Amount}")]
            public class Money
            {
                public int Amount { get; set; }
            }
            """);

    /// <summary>Verifies an internal type, invisible outside the assembly, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InternalTypeIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            internal class Money
            {
                public int Amount { get; set; }
            }
            """);

    /// <summary>Verifies a static class, which has no instances to display, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticClassIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public static class Money
            {
                public static int Amount { get; set; }
            }
            """);
}
