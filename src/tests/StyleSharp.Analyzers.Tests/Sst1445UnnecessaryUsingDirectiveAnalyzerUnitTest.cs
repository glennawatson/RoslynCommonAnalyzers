// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1445UnnecessaryUsingDirectiveAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1445UnnecessaryUsingDirectiveAnalyzer"/> (SST1445 unnecessary using directives).</summary>
public class Sst1445UnnecessaryUsingDirectiveAnalyzerUnitTest
{
    /// <summary>Verifies an unused namespace using is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnusedNamespaceUsingIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            {|SST1445:using System.Text;|}

            public class C
            {
                public int M() => 42;
            }
            """);

    /// <summary>Verifies a using consumed by a simple type reference is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsedNamespaceUsingIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Text;

            public class C
            {
                public string M() => new StringBuilder().ToString();
            }
            """);

    /// <summary>Verifies a using consumed only by an extension-method invocation is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByExtensionMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public class C
            {
                public bool M(int[] values) => values.Any();
            }
            """);

    /// <summary>Verifies a using consumed only by query syntax is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByQuerySyntaxIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public IEnumerable<int> M(int[] values) => from v in values where v > 0 select v;
            }
            """);

    /// <summary>Verifies an unused using static is flagged and a used one is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingStaticUsageIsTrackedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using static System.Math;
            {|SST1445:using static System.Environment;|}

            public class C
            {
                public int M(int value) => Max(value, 0);
            }
            """);

    /// <summary>Verifies an unused alias is flagged and a used one is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AliasUsageIsTrackedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using SB = System.Text.StringBuilder;
            {|SST1445:using SR = System.IO.StringReader;|}

            public class C
            {
                public string M() => new SB().ToString();
            }
            """);

    /// <summary>Verifies a using consumed only by an attribute is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByAttributeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                [Obsolete("old")]
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a using consumed only inside nameof is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByNameofIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Text;

            public class C
            {
                public string M() => nameof(StringBuilder);
            }
            """);

    /// <summary>Verifies a using consumed only from an XML doc cref is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByDocCrefIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Text;

            /// <summary>Builds like <see cref="StringBuilder"/>.</summary>
            public class C
            {
                /// <summary>Does nothing.</summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies an unused using inside a namespace block is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnusedUsingInsideNamespaceIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            namespace N
            {
                {|SST1445:using System.Text;|}

                public class C
                {
                    public int M() => 42;
                }
            }
            """);

    /// <summary>Verifies a using consumed by a base type reference is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByBaseTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C : EventArgs
            {
            }
            """);

    /// <summary>Verifies a using consumed only by a collection-initializer extension Add is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByCollectionInitializerAddIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using N2;

            namespace N2
            {
                public static class StackExtensions
                {
                    public static void Add<T>(this Stack<T> stack, T item) => stack.Push(item);
                }
            }

            public class C
            {
                public Stack<int> M() => new Stack<int> { 1, 2 };
            }
            """);

    /// <summary>Verifies mixed used and unused usings only flag the unused ones.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MixedUsingsOnlyFlagUnusedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            {|SST1445:using System.Collections.Generic;|}
            using System.Text;

            public class C
            {
                public string M() => new StringBuilder().Append(Environment.NewLine).ToString();
            }
            """);

    /// <summary>Verifies a using consumed only by a C# 14 extension-block member invocation is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByExtensionBlockMemberIsCleanAsync()
        => await RunWithExtensionBlocksAsync(
            """
            using Lib.Internal;

            namespace Lib.Internal
            {
                internal static class Ext
                {
                    extension<T>(System.Collections.Generic.IEnumerable<T> source)
                    {
                        public int CountItems() => System.Linq.Enumerable.Count(source);
                    }
                }
            }

            namespace Consumer
            {
                internal static class Use
                {
                    public static int N(System.Collections.Generic.IEnumerable<int> xs) => xs.CountItems();
                }
            }
            """);

    /// <summary>Verifies a using consumed only by a C# 14 extension-block property access is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UsingConsumedByExtensionBlockPropertyIsCleanAsync()
        => await RunWithExtensionBlocksAsync(
            """
            using Lib.Internal;

            namespace Lib.Internal
            {
                internal static class Ext
                {
                    extension(string text)
                    {
                        public bool IsBlank => text.Length == 0;
                    }
                }
            }

            namespace Consumer
            {
                internal static class Use
                {
                    public static bool N(string s) => s.IsBlank;
                }
            }
            """);

    /// <summary>Runs the analyzer verifier with a language version that parses extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunWithExtensionBlocksAsync(string source)
    {
        var test = new Verify.Test { TestCode = source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
