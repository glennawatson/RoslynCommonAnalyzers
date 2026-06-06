// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyText = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DocumentationTextAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the documentation text-quality rules (SST1628/SST1630/SST1631/SST1632).</summary>
public class DocumentationTextAnalyzerUnitTest
{
    /// <summary>Verifies a summary that begins with a lower-case letter is reported (SST1628).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LowerCaseSummaryReportedAsync()
        => await VerifyText.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// {|SST1628:<summary>does the work here.</summary>|}
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a single-word summary is reported (SST1630).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleWordSummaryReportedAsync()
        => await VerifyText.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// {|SST1630:<summary>Singleword</summary>|}
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a summary made up mostly of symbols is reported (SST1631).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SymbolHeavySummaryReportedAsync()
        => await VerifyText.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// {|SST1631:<summary>12 34 56</summary>|}
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a too-short summary is reported (SST1632).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TooShortSummaryReportedAsync()
        => await VerifyText.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// {|SST1632:<summary>A b</summary>|}
                public void M()
                {
                }
            }
            """);
}
