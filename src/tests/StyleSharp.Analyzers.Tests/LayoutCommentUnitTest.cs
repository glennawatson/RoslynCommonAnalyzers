// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
        const string Source = """
            internal class C
            {
                private void M()
                {
                    var a = 1;
                    {|SST1515:// comment|}
                    var b = a;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    var a = 1;

                    // comment
                    var b = a;
                }
            }
            """;
        await VerifyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment followed by a blank line is reported (SST1512) and the blank removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentFollowedByBlankRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M()
                {
                    {|SST1512:// comment|}

                    var a = 1;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    // comment
                    var a = 1;
                }
            }
            """;
        await VerifyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every comment-spacing violation in one document in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void M()
                {
                    var a = 1;
                    {|SST1515:// first|}
                    var b = a;
                    {|SST1515:// second|}
                    var c = b;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    var a = 1;

                    // first
                    var b = a;

                    // second
                    var c = b;
                }
            }
            """;
        await VerifyComment.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment that hugs its code with a blank line above is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WellSpacedCommentIsCleanAsync()
        => await VerifyComment.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M()
                {
                    var a = 1;

                    // comment
                    var b = a;
                }
            }
            """);

    /// <summary>Verifies a comment immediately after a preprocessor directive is not flagged for a missing preceding blank line (SST1515).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentAfterDirectiveIsCleanAsync()
        => await VerifyComment.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(System.Collections.Generic.List<int> items)
                {
            #if NET8_0_OR_GREATER
                    // Use Span-based iteration for zero-allocation enumeration.
                    var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(items);
                    for (var i = 0; i < span.Length; i++)
                    {
                        _ = span[i];
                    }
            #else
                    // Fall back to foreach for older frameworks.
                    foreach (var item in items)
                    {
                        _ = item;
                    }
            #endif
                }
            }
            """);

    /// <summary>Verifies a file header comment keeps the blank separator before using directives (no SST1512).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileHeaderBeforeUsingIsCleanAsync()
        => await VerifyComment.VerifyAnalyzerAsync(
            """
            // Copyright text.

            using System;

            internal class C
            {
                private static string M() => string.Empty;
            }
            """);

    /// <summary>Verifies the standalone-comment helper accepts indentation before a single-line comment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StandaloneCommentHelperAcceptsIndentedCommentAsync()
    {
        var comment = ParseSingleLineComment(
            $$"""
            class C
            {
                // comment
            }{{"\n"}}
            """);
        var text = await comment.SyntaxTree!.GetTextAsync();

        await Assert.That(SingleLineCommentSpacingAnalyzer.IsStandaloneComment(text, comment)).IsTrue();
    }

    /// <summary>Verifies the standalone-comment helper rejects a trailing single-line comment after code.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StandaloneCommentHelperRejectsTrailingCommentAsync()
    {
        var comment = ParseSingleLineComment(
            $$"""
            class C
            {
                void M() { var value = 0; // comment }
            }{{"\n"}}
            """);
        var text = await comment.SyntaxTree!.GetTextAsync();

        await Assert.That(SingleLineCommentSpacingAnalyzer.IsStandaloneComment(text, comment)).IsFalse();
    }

    /// <summary>Verifies a comment that opens an #if branch after a blank line is not flagged (SST1512/SST1515 exempt the directive boundary).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentAfterConditionalDirectiveWithBlankNotFlaggedAsync()
        => await VerifyComment.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M()
                {
            #if true

                    // comment opening the branch
                    System.Console.WriteLine();
            #endif
                }
            }
            """);

    /// <summary>Verifies the file header is still exempt when the whole body is compiled out.</summary>
    /// <remarks>
    /// Under a target framework where the condition is false, the file has no token but the end-of-file
    /// one. Skipping that zero-width token would put the header after the "first" token at position 0 and
    /// collapse the header exemption, reporting the copyright banner of every file compiled out.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FileHeaderIsExemptWhenTheWholeBodyIsCompiledOutAsync()
        => await VerifyComment.VerifyAnalyzerAsync(
            """
            // Copyright (c) Contributors. All rights reserved.
            // Licensed under the MIT license.
            #if SOME_UNDEFINED_SYMBOL
            public class C
            {
            }
            #endif
            """);

    /// <summary>Parses the first single-line comment trivia from the supplied source.</summary>
    /// <param name="source">The source containing the target comment.</param>
    /// <returns>The parsed single-line comment trivia.</returns>
    private static SyntaxTrivia ParseSingleLineComment(string source)
        => SyntaxFactory.ParseCompilationUnit(source).DescendantTrivia().First(static trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia));
}
