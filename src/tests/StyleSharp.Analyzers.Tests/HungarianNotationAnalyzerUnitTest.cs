// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyHungarian = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.HungarianNotationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the Hungarian-notation rule (SST1305).</summary>
public class HungarianNotationAnalyzerUnitTest
{
    /// <summary>Verifies a Hungarian-notation parameter is reported (SST1305).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HungarianParameterReportedAsync()
        => await VerifyHungarian.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(int {|SST1305:iCount|})
                {
                }
            }
            """);

    /// <summary>Verifies an ordinary camelCase parameter is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CamelCaseParameterIsCleanAsync()
        => await VerifyHungarian.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(int isEnabled)
                {
                }
            }
            """);
}
