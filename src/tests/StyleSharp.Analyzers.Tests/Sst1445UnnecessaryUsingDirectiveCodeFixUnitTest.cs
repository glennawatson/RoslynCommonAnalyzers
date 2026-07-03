// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1445UnnecessaryUsingDirectiveAnalyzer,
    StyleSharp.Analyzers.Sst1445UnnecessaryUsingDirectiveCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1445UnnecessaryUsingDirectiveCodeFixProvider"/> (SST1445 remove unnecessary using).</summary>
public class Sst1445UnnecessaryUsingDirectiveCodeFixUnitTest
{
    /// <summary>An unused using between used code.</summary>
    private const string SingleUnusedSource = """
        using System;
        {|SST1445:using System.Text;|}

        public class C
        {
            public void M() => Console.WriteLine("x");
        }
        """;

    /// <summary>The single-unused source after the fix.</summary>
    private const string SingleUnusedFixed = """
        using System;

        public class C
        {
            public void M() => Console.WriteLine("x");
        }
        """;

    /// <summary>An unused first using carrying a comment banner.</summary>
    private const string BannerSource = """
        // banner comment
        {|SST1445:using System.Text;|}

        public class C
        {
            public int M() => 42;
        }
        """;

    /// <summary>The banner source after the fix.</summary>
    private const string BannerFixed = """
        // banner comment

        public class C
        {
            public int M() => 42;
        }
        """;

    /// <summary>Two unused usings around a used one.</summary>
    private const string FixAllSource = """
        {|SST1445:using System.Collections.Generic;|}
        using System;
        {|SST1445:using System.Text;|}

        public class C
        {
            public void M() => Console.WriteLine("x");
        }
        """;

    /// <summary>The fix-all source after both removals.</summary>
    private const string FixAllFixed = """
        using System;

        public class C
        {
            public void M() => Console.WriteLine("x");
        }
        """;

    /// <summary>Verifies the fix removes an unused using and its line.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RemovesUnusedUsingAsync()
        => await Verify.VerifyCodeFixAsync(SingleUnusedSource, SingleUnusedFixed);

    /// <summary>Verifies removing the first using keeps a leading comment banner.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task KeepsLeadingCommentBannerAsync()
        => await Verify.VerifyCodeFixAsync(BannerSource, BannerFixed);

    /// <summary>Verifies fix-all removes every unused using in the document.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllRemovesEveryUnusedUsingAsync()
        => await Verify.VerifyCodeFixAsync(FixAllSource, FixAllFixed);
}
