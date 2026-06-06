// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySectionText = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DocumentationTextAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1627 (documentation section text must not be empty).</summary>
public class DocumentationSectionTextAnalyzerUnitTest
{
    /// <summary>Verifies empty remarks are reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EmptyRemarksAreReportedAsync()
        => await VerifySectionText.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does useful work.</summary>
                /// {|SST1627:<remarks></remarks>|}
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies non-empty remarks and empty summaries are not SST1627 diagnostics.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonEmptyRemarksAreCleanAsync()
        => await VerifySectionText.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does useful work.</summary>
                /// <remarks>Additional information.</remarks>
                public void M()
                {
                }
            }
            """);
}
