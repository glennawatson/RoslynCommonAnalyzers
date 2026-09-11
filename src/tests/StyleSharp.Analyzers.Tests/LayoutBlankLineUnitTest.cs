// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyBlanks = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1507MultipleBlankLinesAnalyzer,
    StyleSharp.Analyzers.Sst1507MultipleBlankLinesCodeFixProvider>;
using VerifySpacing = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1516ElementSpacingAnalyzer,
    StyleSharp.Analyzers.Sst1516ElementSpacingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the blank-line layout rules (SST1507/SST1516).</summary>
public class LayoutBlankLineUnitTest
{
    /// <summary>The diagnostic id of the multiple-blank-lines rule.</summary>
    private const string MultipleBlankLinesDiagnosticId = "SST1507";

    /// <summary>Verifies two consecutive blank lines are reported (SST1507) and collapsed to one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleBlankLinesCollapsedAsync()
    {
        const string Source = """
            internal class C
            {
                private int a;


                private int b;
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private int a;

                private int b;
            }
            """;
        const int ExtraBlankLine = 5;
        const int LineAfterExtraBlank = 6;
        await VerifyBlanks.VerifyCodeFixAsync(
            Source,
            VerifyBlanks.Diagnostic(MultipleBlankLinesDiagnosticId).WithSpan(ExtraBlankLine, 1, LineAfterExtraBlank, 1),
            FixedSource);
    }

    /// <summary>Verifies Fix All collapses every multiple-blank-line run in the document in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryBlankRunOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private int a;


                private int b;


                private int c;
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private int a;

                private int b;

                private int c;
            }
            """;
        const int FirstExtraBlankLine = 5;
        const int LineAfterFirstExtraBlank = 6;
        const int SecondExtraBlankLine = 8;
        const int LineAfterSecondExtraBlank = 9;
        await VerifyBlanks.VerifyCodeFixAsync(
            Source,
            [
                VerifyBlanks.Diagnostic(MultipleBlankLinesDiagnosticId).WithSpan(FirstExtraBlankLine, 1, LineAfterFirstExtraBlank, 1),
                VerifyBlanks.Diagnostic(MultipleBlankLinesDiagnosticId).WithSpan(SecondExtraBlankLine, 1, LineAfterSecondExtraBlank, 1),
            ],
            FixedSource);
    }

    /// <summary>Verifies a single blank line between members is allowed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleBlankLineIsCleanAsync() =>
        VerifyBlanks.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int a;

                private int b;
            }
            """);

    /// <summary>Verifies adjacent members without a blank line are reported (SST1516) and separated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdjacentMembersSeparatedAsync()
    {
        const string Source = """
            internal class C
            {
                private void A()
                {
                }
                {|SST1516:private|} void B()
                {
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void A()
                {
                }

                private void B()
                {
                }
            }
            """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All separates every adjacent member pair in one pass (SST1516).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void A()
                {
                }
                {|SST1516:private|} void B()
                {
                }
                {|SST1516:private|} void D()
                {
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void A()
                {
                }

                private void B()
                {
                }

                private void D()
                {
                }
            }
            """;
        await VerifySpacing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies members already separated by a blank line are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SeparatedMembersAreCleanAsync() =>
        VerifySpacing.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void A()
                {
                }

                private void B()
                {
                }
            }
            """);

    /// <summary>Verifies a single blank line after a conditional directive is not flagged (SST1507 does not treat the directive line as blank).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BlankLineAfterConditionalDirectiveNotFlaggedAsync() =>
        VerifyBlanks.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M()
                {
            #if true

                    System.Console.WriteLine();
            #endif
                }
            }
            """);
}
