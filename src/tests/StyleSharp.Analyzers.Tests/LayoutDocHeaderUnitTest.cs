// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDoc = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.DocumentationHeaderSpacingAnalyzer,
    StyleSharp.Analyzers.DocumentationHeaderSpacingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the documentation-header spacing rules (SST1506/SST1514).</summary>
public class LayoutDocHeaderUnitTest
{
    /// <summary>Verifies a blank line between a header and its element is reported (SST1506) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankAfterHeaderRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                /// <summary>Does A.</summary>

                {|SST1506:private|} void A()
                {
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                /// <summary>Does A.</summary>
                private void A()
                {
                }
            }
            """;
        await VerifyDoc.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a header not preceded by a blank line is reported (SST1514) and separated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingBlankBeforeHeaderInsertedAsync()
    {
        const string Source = """
            internal class C
            {
                private int x;
                /// <summary>Gets X.</summary>
                {|SST1514:public|} int X => x;
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private int x;

                /// <summary>Gets X.</summary>
                public int X => x;
            }
            """;
        await VerifyDoc.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a correctly spaced documentation header is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WellSpacedHeaderIsCleanAsync()
        => await VerifyDoc.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int x;

                /// <summary>Gets X.</summary>
                public int X => x;
            }
            """);
}
