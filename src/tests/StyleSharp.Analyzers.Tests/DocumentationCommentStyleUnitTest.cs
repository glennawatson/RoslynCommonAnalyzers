// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDocStyle = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DocumentationCommentStyleAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the documentation-comment style rules (SST1626/SST1651).</summary>
public class DocumentationCommentStyleUnitTest
{
    /// <summary>Verifies a placeholder documentation element is reported (SST1651).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlaceholderElementReportedAsync()
        => await VerifyDocStyle.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>{|SST1651:<placeholder>TODO</placeholder>|}</summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a documentation-style comment that documents nothing is reported (SST1626).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MisplacedDocumentationCommentReportedAsync()
        => await VerifyDocStyle.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    /// note
                    System.Console.WriteLine();
                }
            }
            """,
            VerifyDocStyle.Diagnostic("SST1626").WithSpan(5, 12, 6, 1));

    /// <summary>Verifies a documentation comment that documents a member is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DocumentationOnMemberIsCleanAsync()
        => await VerifyDocStyle.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does the work.</summary>
                public void M()
                {
                }
            }
            """);
}
