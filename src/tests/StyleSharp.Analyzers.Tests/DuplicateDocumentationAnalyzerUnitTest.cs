// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDuplicate = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DuplicateDocumentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the duplicate documentation rule (SST1625).</summary>
public class DuplicateDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies documentation copied between elements is reported (SST1625).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateDocumentationReportedAsync()
        => await VerifyDuplicate.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>The same text.</summary>
                /// {|SST1625:<param name="x">The same text.</param>|}
                public void M(int x)
                {
                }
            }
            """);

    /// <summary>Verifies distinct documentation text is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DistinctDocumentationIsCleanAsync()
        => await VerifyDuplicate.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does the work.</summary>
                /// <param name="x">The input value.</param>
                public void M(int x)
                {
                }
            }
            """);
}
