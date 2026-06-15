// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyChain = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ChainedBlockSpacingAnalyzer,
    StyleSharp.Analyzers.ChainedBlockSpacingCodeFixProvider>;
using VerifyFileEnd = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1518FileEndingAnalyzer,
    StyleSharp.Analyzers.Sst1518FileEndingCodeFixProvider>;
using VerifyFileStart = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1517FileStartBlankLinesAnalyzer,
    StyleSharp.Analyzers.Sst1517FileStartBlankLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the file-boundary and chained-block layout rules (SST1510/SST1511/SST1517/SST1518).</summary>
public class LayoutFileAndChainUnitTest
{
    /// <summary>Verifies a blank line at the start of the file is reported (SST1517) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLinesAtFileStartRemovedAsync()
    {
        const string Source = $$"""

            internal class C
            {
            }{{"\n"}}
            """;
        const string FixedSource = $$"""
            internal class C
            {
            }{{"\n"}}
            """;
        await VerifyFileStart.VerifyCodeFixAsync(
            Source,
            VerifyFileStart.Diagnostic("SST1517").WithSpan(1, 1, 2, 1),
            FixedSource);
    }

    /// <summary>Verifies a missing final newline is reported (SST1518) and added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingFinalNewlineAddedAsync()
    {
        // Normalized to line feeds so the document, and the break the fix copies from it, stay the same
        // on a carriage-return checkout. The file-ending fix reuses the document's break verbatim.
        var source = """
            internal class C
            {
            }
            """.ReplaceLineEndings("\n");
        var fixedSource = $$"""
            internal class C
            {
            }{{"\n"}}
            """.ReplaceLineEndings("\n");
        await VerifyFileEnd.VerifyCodeFixAsync(
            source,
            VerifyFileEnd.Diagnostic("SST1518").WithSpan(3, 2, 3, 2),
            fixedSource);
    }

    /// <summary>Verifies a blank line before 'else' is reported (SST1510) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankBeforeElseRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                    }

                    {|SST1510:else|}
                    {
                    }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                    }
                    else
                    {
                    }
                }
            }
            """;
        await VerifyChain.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line before a do/while footer is reported (SST1511) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankBeforeWhileFooterRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool x)
                {
                    do
                    {
                    }

                    {|SST1511:while|} (x);
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool x)
                {
                    do
                    {
                    }
                    while (x);
                }
            }
            """;
        await VerifyChain.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes the blank line before every chained keyword (SST1510/SST1511) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                    }

                    {|SST1510:else|}
                    {
                    }

                    do
                    {
                    }

                    {|SST1511:while|} (x);

                    if (x)
                    {
                    }

                    {|SST1510:else|}
                    {
                    }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                    }
                    else
                    {
                    }

                    do
                    {
                    }
                    while (x);

                    if (x)
                    {
                    }
                    else
                    {
                    }
                }
            }
            """;
        await VerifyChain.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an 'else' that directly follows the if block is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdjacentElseIsCleanAsync()
        => await VerifyChain.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                    }
                    else
                    {
                    }
                }
            }
            """);
}
