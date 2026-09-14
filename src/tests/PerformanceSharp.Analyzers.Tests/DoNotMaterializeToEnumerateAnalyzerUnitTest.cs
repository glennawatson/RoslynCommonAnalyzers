// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Analyze = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<PerformanceSharp.Analyzers.Psh1120DoNotMaterializeToEnumerateAnalyzer>;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1120DoNotMaterializeToEnumerateAnalyzer,
    PerformanceSharp.Analyzers.Psh1120DoNotMaterializeToEnumerateCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1120DoNotMaterializeToEnumerateAnalyzer"/> (PSH1120 materialize-to-enumerate).</summary>
public class DoNotMaterializeToEnumerateAnalyzerUnitTest
{
    /// <summary>Checks receiver traversal through indexing, parentheses, and nonidentifier roots.</summary>
    /// <param name="receiver">The materialized sequence expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("groups[0]")]
    [Arguments("(items)")]
    [Arguments("this.Items")]
    [Arguments("new int[] { 1 }")]
    [Arguments("GetItems()")]
    [Arguments("base.Values")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReceiverShapesAreReportedAsync(string receiver) =>
        Analyze.VerifyAnalyzerAsync($$"""
            using System.Collections.Generic;
            using System.Linq;
            class Base { protected int[] Values; }
            class C : Base
            {
                int[] Items;
                int[] GetItems() => Items;
                void M(int[] items, int[][] groups)
                {
                    foreach (var item in {{receiver}}.{|PSH1120:ToArray|}()) { _ = item; }
                }
            }
            """);

    /// <summary>Checks the root identifier and fallback member name prevent unsafe snapshot removal.</summary>
    /// <param name="receiver">The sequence expression.</param>
    /// <param name="body">The loop body mentioning its root.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("groups[0]", "groups[0] = null;")]
    [Arguments("(items)", "items[0] = 1;")]
    [Arguments("this.Items", "Items = null;")]
    [Arguments("this.GetItems()", "GetItems();")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReceiverRootMentionsPreserveSnapshotsAsync(string receiver, string body) =>
        Analyze.VerifyAnalyzerAsync($$"""
            using System.Linq;
            class C
            {
                int[] Items;
                int[] GetItems() => Items;
                void M(int[] items, int[][] groups)
                {
                    foreach (var item in {{receiver}}.ToArray()) { {{body}} }
                }
            }
            """);

    /// <summary>Checks calls that do not bind to the one-parameter Enumerable extension are ignored.</summary>
    /// <param name="source">The loop and candidate method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System.Linq; class C { int[] ToArray() => null; void M() { foreach (var x in ToArray()) {} } }")]
    [Arguments("using System.Linq; class C { int[] ToArray() => null; void M() { foreach (var x in this.ToArray()) {} } }")]
    [Arguments("using System.Linq; class C { void M(int[] items) { foreach (var x in items.Reverse()) {} } }")]
    [Arguments("using System.Linq; class C { void M(int[] items) { foreach (var x in Enumerable.ToArray(items)) {} } }")]
    [Arguments("using System.Linq; static class Extensions { public static int[] ToArray(this string source) => null; } class C { void M(string text) { foreach (var x in text.ToArray()) {} } }")]
    [Arguments("""
        using System.Linq;
        static class Extensions { public static int[] ToArray(this int source, int count = 0) => null; }
        class C { void M(int value) { foreach (var x in value.ToArray()) {} } }
        """)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnrelatedMaterializationIsCleanAsync(string source) =>
        Analyze.VerifyAnalyzerAsync(source);

    /// <summary>Checks an unresolved receiver does not produce a materialization suggestion.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedMaterializationIsCleanAsync() =>
        new Analyze.Test { TestCode = "using System.Linq; class C { void M() { foreach (var x in missing.ToArray()) {} } }", CompilerDiagnostics = CompilerDiagnostics.None }
            .RunAsync(CancellationToken.None);

    /// <summary>Checks materialization syntax requires a parameterless simple member access.</summary>
    /// <param name="expression">The invocation expression.</param>
    /// <param name="expected">Whether the syntax is a candidate.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("items.ToArray()", true)]
    [Arguments("items.ToList()", true)]
    [Arguments("items.ToList(1)", false)]
    [Arguments("ToList()", false)]
    [Arguments("items->ToList()", false)]
    [Arguments("items.Where()", false)]
    public async Task MaterializationShapeRequiresSimpleSourceOnlyCallAsync(string expression, bool expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        await Assert.That(Psh1120DoNotMaterializeToEnumerateAnalyzer.IsMaterializeInvocationShape(invocation)).IsEqualTo(expected);
    }

    /// <summary>Checks missing Enumerable metadata is cached and leaves repeated candidate loops clean.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingEnumerableTypeIsCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { foreach (var x in items.ToArray()) {} foreach (var x in items.ToList()) {} } }");
        var compilation = CSharpCompilation.Create(nameof(MissingEnumerableTypeIsCleanAsync), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Psh1120DoNotMaterializeToEnumerateAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a ToList call at the end of a LINQ chain is flagged and the fix drops it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToListOnWhereChainIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(IEnumerable<int> items)
                                  {
                                      var total = 0;
                                      foreach (var x in items.Where(i => i > 0).{|PSH1120:ToList|}())
                                      {
                                          total += x;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(IEnumerable<int> items)
                                       {
                                           var total = 0;
                                           foreach (var x in items.Where(i => i > 0))
                                           {
                                               total += x;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a ToArray call directly on the source is flagged and the fix drops it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToArrayOnSourceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(IEnumerable<int> items)
                                  {
                                      var total = 0;
                                      foreach (var x in items.{|PSH1120:ToArray|}())
                                      {
                                          total += x;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(IEnumerable<int> items)
                                       {
                                           var total = 0;
                                           foreach (var x in items)
                                           {
                                               total += x;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a loop that mutates the materialized source stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MutatedSourceIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(List<int> items)
                {
                    foreach (var x in items.ToList())
                    {
                        if (x > 0)
                        {
                            items.Remove(x);
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies any mention of the source identifier in the loop body suppresses the report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BodyMentionOfSourceIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(List<int> items)
                {
                    var total = 0;
                    foreach (var x in items.ToList())
                    {
                        total += x + items.Count;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies the guard follows the root identifier through a LINQ chain to the source.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RootIdentifierGuardsThroughChainAsync() =>
        VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(List<int> items)
                {
                    foreach (var x in items.Where(i => i > 0).ToList())
                    {
                        items.Add(x);
                    }
                }
            }
            """);

    /// <summary>Verifies enumerating a variable that holds the materialized copy stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MaterializedLocalIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> items)
                {
                    var total = 0;
                    var list = items.ToList();
                    foreach (var x in list)
                    {
                        total += x;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an await foreach stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AwaitForeachIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class AsyncSequence : IAsyncEnumerable<int>
            {
                public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                    => throw new NotSupportedException();
            }

            public static class AsyncSequenceExtensions
            {
                public static AsyncSequence ToList(this AsyncSequence source) => source;
            }

            public class C
            {
                public async Task M(AsyncSequence items)
                {
                    await foreach (var x in items.ToList())
                    {
                        _ = x;
                    }
                }
            }
            """);

    /// <summary>Verifies a custom ToList extension that takes an argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ToListWithArgumentIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public static class SequenceExtensions
            {
                public static List<int> ToList(this IEnumerable<int> source, int capacity)
                {
                    var list = new List<int>(capacity);
                    list.AddRange(source);
                    return list;
                }
            }

            public class C
            {
                public int M(IEnumerable<int> items)
                {
                    var total = 0;
                    foreach (var x in items.ToList(4))
                    {
                        total += x;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
