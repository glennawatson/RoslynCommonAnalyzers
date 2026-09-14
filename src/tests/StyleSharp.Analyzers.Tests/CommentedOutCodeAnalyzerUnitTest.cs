// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyCommentedCode = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1148CommentedOutCodeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1148 (remove commented-out code).</summary>
public class CommentedOutCodeAnalyzerUnitTest
{
    /// <summary>Verifies each statement signal works without requiring a complete statement.</summary>
    /// <param name="comment">The comment containing a code signal.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("// return value")]
    [Arguments("// throw error")]
    [Arguments("// var value = result")]
    [Arguments("// if (ready)")]
    [Arguments("// for (int i = 0)")]
    [Arguments("// while (ready)")]
    [Arguments("// if (ready) {")]
    [Arguments("// end }")]
    [Arguments("// return;   \t")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CodeSignalsAreReportedAsync(string comment) =>
        VerifyCommentedCode.VerifyAnalyzerAsync($"class C {{\n{{|SST1148:{comment}|}}\n}}");

    /// <summary>Verifies short prose and non-code markers remain silent even beside code-like punctuation.</summary>
    /// <param name="comment">The comment without a reportable signal.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("// word")]
    [Arguments("// HACK: revisit;")]
    [Arguments("// ----;")]
    [Arguments("// ====;")]
    [Arguments("// ****;")]
    [Arguments("// ret")]
    [Arguments("//   ")]
    [Arguments("// returnValue")]
    [Arguments("// throwaway")]
    [Arguments("// variable")]
    [Arguments("// if ready")]
    [Arguments("// for each item")]
    [Arguments("// while waiting")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MarkerAndKeywordNearMissesAreCleanAsync(string comment) =>
        VerifyCommentedCode.VerifyAnalyzerAsync($"class C {{\n{comment}\n}}");

    /// <summary>Verifies a commented statement is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CommentedStatementIsReportedAsync() =>
        VerifyCommentedCode.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonCodeCommentsAreCleanAsync() =>
        VerifyCommentedCode.VerifyAnalyzerAsync(
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

    /// <summary>Verifies real commented-out statements (assignment, call, bare jump) are still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CommentedStatementsAreReportedAsync() =>
        VerifyCommentedCode.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    {|SST1148:// _total = 0;|}
                    {|SST1148:// Compute(value);|}
                    {|SST1148:// break;|}
                }
            }
            """);
}
