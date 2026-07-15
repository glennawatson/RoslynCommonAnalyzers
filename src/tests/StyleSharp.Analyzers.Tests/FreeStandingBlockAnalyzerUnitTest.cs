// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBlock = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.EmptyCodeAnalyzer>;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.EmptyCodeAnalyzer,
    StyleSharp.Analyzers.Sst1138FreeStandingBlockCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1138 (a free-standing block that declares nothing).</summary>
public class FreeStandingBlockAnalyzerUnitTest
{
    /// <summary>A method with a free-standing block that only nests statements.</summary>
    private const string FreeStandingSource = """
        using System;

        public sealed class C
        {
            public void M()
            {
                {|SST1138:{
                    Console.WriteLine("a");
                    Console.WriteLine("b");
                }|}
            }
        }
        """;

    /// <summary>The method after splicing the block's statements out.</summary>
    private const string FreeStandingFixed = """
        using System;

        public sealed class C
        {
            public void M()
            {
                Console.WriteLine("a");
                Console.WriteLine("b");
            }
        }
        """;

    /// <summary>Verifies a free-standing block that declares nothing is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FreeStandingBlockIsReportedAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(FreeStandingSource);

    /// <summary>Verifies a block that declares a local is clean: it scopes that local.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockThatScopesALocalIsCleanAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    {
                        var x = 1;
                        Console.WriteLine(x);
                    }
                }
            }
            """);

    /// <summary>Verifies a comment-only block is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentOnlyBlockIsCleanAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M()
                {
                    {
                        // intentionally left blank
                    }
                }
            }
            """);

    /// <summary>Verifies a method body itself is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodBodyIsCleanAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M() => Console.WriteLine("a");
            }
            """);

    /// <summary>Verifies the fix splices the statements into the enclosing block.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixSplicesStatementsAsync()
        => await VerifyFix.VerifyCodeFixAsync(FreeStandingSource, FreeStandingFixed);
}
