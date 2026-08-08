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

    /// <summary>Verifies a block scoping a pattern variable and an out variable next to a sibling that reuses the names is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockThatScopesPatternAndOutVariablesIsCleanAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(object o)
                {
                    {
                        _ = o is int i;
                        _ = int.TryParse("123", out var j);
                    }
                    {
                        int i = 0;
                        int j = 0;
                        _ = i + j;
                    }
                }
            }
            """);

    /// <summary>Verifies a block scoping a deconstruction designation is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockThatScopesADeconstructionDesignationIsCleanAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M((int, int) t)
                {
                    {
                        (var a, var b) = t;
                        _ = a + b;
                    }
                }
            }
            """);

    /// <summary>Verifies a block whose 'if', 'switch', or 'lock' header declares a variable is clean: the name outlives the statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockThatScopesAHeaderVariableIsCleanAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private readonly object _gate = new();

                public void M(object o)
                {
                    {
                        if (o is int i)
                        {
                            Console.WriteLine(i);
                        }
                    }
                    {
                        switch (o is string s ? 1 : 2)
                        {
                            default:
                                break;
                        }
                    }
                    {
                        lock (Gate(o is bool b))
                        {
                            Console.WriteLine("locked");
                        }
                    }
                }

                private object Gate(bool value) => _gate;
            }
            """);

    /// <summary>Verifies a block whose loop or resource header declares a variable is still reported: that name dies with the statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockWhoseLoopHeaderScopesAVariableIsReportedAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(object o)
                {
                    {|SST1138:{
                        while (o is int i)
                        {
                            Console.WriteLine(i);
                            break;
                        }

                        foreach (var e in Items(o is string s))
                        {
                            Console.WriteLine(e);
                        }

                        using (Scope(o is bool b))
                        {
                            Console.WriteLine("scoped");
                        }
                    }|}
                }

                private static string[] Items(bool value) => value ? ["a"] : [];

                private static IDisposable Scope(bool value) => new System.IO.MemoryStream();
            }
            """);

    /// <summary>Verifies a block is still reported when the only designations sit inside a nested scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockWithDesignationsInNestedScopesIsReportedAsync()
        => await VerifyBlock.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(object o)
                {
                    {|SST1138:{
                        Run(() => { _ = o is int i; });
                        Console.WriteLine(o switch { int i => i, _ => 0 });
                    }|}
                }

                private static void Run(Action action)
                {
                    action();
                }
            }
            """);
}
