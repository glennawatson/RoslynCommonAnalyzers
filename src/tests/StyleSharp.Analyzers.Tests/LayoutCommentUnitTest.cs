// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyComment = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.SingleLineCommentSpacingAnalyzer,
    StyleSharp.Analyzers.SingleLineCommentSpacingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the single-line comment spacing rules (SST1512/SST1515).</summary>
public class LayoutCommentUnitTest
{
    /// <summary>Verifies a comment not preceded by a blank line is reported (SST1515) and separated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentMissingBlankBeforeInsertedAsync()
    {
        const string Source = "internal class C\n{\n    private void M()\n    {\n        var a = 1;\n        {|SST1515:// comment|}\n        var b = a;\n    }\n}";
        const string FixedSource = "internal class C\n{\n    private void M()\n    {\n        var a = 1;\n\n        // comment\n        var b = a;\n    }\n}";
        await VerifyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment followed by a blank line is reported (SST1512) and the blank removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentFollowedByBlankRemovedAsync()
    {
        const string Source = "internal class C\n{\n    private void M()\n    {\n        {|SST1512:// comment|}\n\n        var a = 1;\n    }\n}";
        const string FixedSource = "internal class C\n{\n    private void M()\n    {\n        // comment\n        var a = 1;\n    }\n}";
        await VerifyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment that hugs its code with a blank line above is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WellSpacedCommentIsCleanAsync()
        => await VerifyComment.VerifyAnalyzerAsync(
            "internal class C\n{\n    private void M()\n    {\n        var a = 1;\n\n        // comment\n        var b = a;\n    }\n}");
}
