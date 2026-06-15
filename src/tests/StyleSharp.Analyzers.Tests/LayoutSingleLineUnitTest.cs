// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAccessor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1504AccessorConsistencyAnalyzer,
    StyleSharp.Analyzers.Sst1504AccessorConsistencyCodeFixProvider>;
using VerifyElement = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1502SingleLineElementAnalyzer,
    StyleSharp.Analyzers.SingleLineBlockReflowCodeFixProvider>;
using VerifyStatement = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1501SingleLineStatementAnalyzer,
    StyleSharp.Analyzers.SingleLineBlockReflowCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the single-line layout rules (SST1501/SST1502/SST1504).</summary>
public class LayoutSingleLineUnitTest
{
    /// <summary>Verifies a single-line embedded block is reported (SST1501) and expanded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineEmbeddedBlockExpandedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool b)
                {
                    if (b) {|SST1501:{|} b = false; }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool b)
                {
                    if (b)
                    {
                        b = false;
                    }
                }
            }
            """;
        await VerifyStatement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All expands every single-line embedded block (SST1501) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool b)
                {
                    if (b) {|SST1501:{|} b = false; }

                    if (b) {|SST1501:{|} b = true; }

                    if (b) {|SST1501:{|} b = false; }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool b)
                {
                    if (b)
                    {
                        b = false;
                    }

                    if (b)
                    {
                        b = true;
                    }

                    if (b)
                    {
                        b = false;
                    }
                }
            }
            """;
        await VerifyStatement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-line embedded block is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineEmbeddedBlockIsCleanAsync()
        => await VerifyStatement.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(bool b)
                {
                    if (b)
                    {
                        b = false;
                    }
                }
            }
            """);

    /// <summary>Verifies a single-line method body is reported (SST1502) and expanded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineMethodBodyExpandedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M() {|SST1502:{|} System.Console.WriteLine(); }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    System.Console.WriteLine();
                }
            }
            """;
        await VerifyElement.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty single-line body is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptySingleLineBodyIsCleanAsync()
        => await VerifyElement.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M() { }
            }
            """);

    /// <summary>Verifies mixed single-line and multi-line accessors are reported (SST1504) and made consistent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedAccessorsExpandedAsync()
    {
        const string Source = """
            internal class C
            {
                private int x;

                public int X
                {|SST1504:{|}
                    get { return x; }
                    set
                    {
                        x = value;
                    }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private int x;

                public int X
                {
                    get
                    {
                        return x;
                    }
                    set
                    {
                        x = value;
                    }
                }
            }
            """;
        await VerifyAccessor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All makes every inconsistent accessor list in the document consistent in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryAccessorOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private int x;
                private int y;

                public int X
                {|SST1504:{|}
                    get { return x; }
                    set
                    {
                        x = value;
                    }
                }

                public int Y
                {|SST1504:{|}
                    get { return y; }
                    set
                    {
                        y = value;
                    }
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private int x;
                private int y;

                public int X
                {
                    get
                    {
                        return x;
                    }
                    set
                    {
                        x = value;
                    }
                }

                public int Y
                {
                    get
                    {
                        return y;
                    }
                    set
                    {
                        y = value;
                    }
                }
            }
            """;
        await VerifyAccessor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies consistently single-line accessors are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsistentAccessorsAreCleanAsync()
        => await VerifyAccessor.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int x;

                public int X
                {
                    get { return x; }
                    set { x = value; }
                }
            }
            """);
}
