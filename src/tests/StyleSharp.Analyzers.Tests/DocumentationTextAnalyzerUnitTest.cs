// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyText = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DocumentationTextAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the documentation text-quality rules (SST1628/SST1630/SST1631/SST1632).</summary>
public class DocumentationTextAnalyzerUnitTest
{
    /// <summary>Verifies a summary that captions one code form is not measured as prose.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The spacing and the ratio of letters to symbols belong to the language, not the author, so neither
    /// SST1630 nor SST1631 says anything about the summary.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CodeCaptionSummaryIsCleanAsync() =>
        VerifyText.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary><c>string.IsNullOrEmpty(text.Trim())</c>.</summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a summary mixing prose with a code element is still measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SummaryWithProseAroundACodeElementIsMeasuredAsync() =>
        VerifyText.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// {|SST1631:<summary>Calls <c>M()</c> ;;;;;;;;;;;;;;;;;;;;.</summary>|}
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a summary that begins with a lower-case letter is reported (SST1628).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LowerCaseSummaryReportedAsync() =>
        VerifyText.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleWordSummaryReportedAsync() =>
        VerifyText.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SymbolHeavySummaryReportedAsync() =>
        VerifyText.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TooShortSummaryReportedAsync() =>
        VerifyText.VerifyAnalyzerAsync(
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
