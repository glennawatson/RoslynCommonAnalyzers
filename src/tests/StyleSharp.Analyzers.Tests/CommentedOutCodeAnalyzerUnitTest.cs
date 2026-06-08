// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCommentedCode = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1148CommentedOutCodeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1148 (remove commented-out code).</summary>
public class CommentedOutCodeAnalyzerUnitTest
{
    /// <summary>Verifies a commented statement is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CommentedStatementIsReportedAsync()
        => await VerifyCommentedCode.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    {|SST1148:// return;|}
                }
            }
            """);

    /// <summary>Verifies prose, task markers, documentation, and a file header are ignored.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonCodeCommentsAreCleanAsync()
        => await VerifyCommentedCode.VerifyAnalyzerAsync(
            """
            // Copyright information.
            public class C
            {
                /// <summary>Does work.</summary>
                public void M()
                {
                    // Compute the running total.
                    // TODO: handle another case.
                    // https://example.com/docs
                }
            }
            """);
}
