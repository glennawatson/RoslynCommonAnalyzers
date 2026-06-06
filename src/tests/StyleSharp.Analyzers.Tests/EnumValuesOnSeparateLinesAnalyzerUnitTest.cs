// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEnumLines = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.EnumValuesOnSeparateLinesAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the enum-values-on-separate-lines rule (SST1136).</summary>
public class EnumValuesOnSeparateLinesAnalyzerUnitTest
{
    /// <summary>Verifies enum members sharing a line are reported (SST1136).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameLineEnumMembersReportedAsync()
        => await VerifyEnumLines.VerifyAnalyzerAsync(
            """
            internal enum E
            {
                A, {|SST1136:B|}, {|SST1136:C|}
            }
            """);

    /// <summary>Verifies enum members on separate lines are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateLineEnumMembersAreCleanAsync()
        => await VerifyEnumLines.VerifyAnalyzerAsync(
            """
            internal enum E
            {
                A,
                B,
                C,
            }
            """);
}
