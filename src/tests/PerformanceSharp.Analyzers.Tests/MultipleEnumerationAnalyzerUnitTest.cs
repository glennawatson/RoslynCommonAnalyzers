// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1125MultipleEnumerationAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1125MultipleEnumerationAnalyzer"/> (PSH1125 multiple enumeration).</summary>
public class MultipleEnumerationAnalyzerUnitTest
{
    /// <summary>The cached primitive reference set for compilations without LINQ.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Checks foreach reporting does not depend on LINQ being present in the target framework.</summary>
    /// <param name="body">The candidate sequence uses.</param>
    /// <param name="expectedCount">The expected number of repeated-enumeration diagnostics.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("foreach (var x in source) {} foreach (var x in source) {}", 1)]
    [Arguments("_ = source.Count(); _ = source.Count();", 0)]
    public async Task MissingLinqStillRecognizesForeachAsync(string body, int expectedCount)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M(System.Collections.Generic.IEnumerable<int> source) {{ {body} }} }}");
        var compilation = CSharpCompilation.Create("WithoutLinq", [tree], CoreReferences);
        var diagnostics = await compilation.WithAnalyzers([new Psh1125MultipleEnumerationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expectedCount);
        if (expectedCount > 0)
        {
            await Assert.That(diagnostics[0].Id).IsEqualTo("PSH1125");
        }
    }

    /// <summary>Checks unsupported type syntax is rejected before semantic binding.</summary>
    /// <param name="source">The type spelling.</param>
    /// <param name="expected">Whether the spelling names an enumerable contract.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("int", false)]
    [Arguments("IEnumerable<int>[]", false)]
    [Arguments("global::IEnumerable", false)]
    [Arguments("IEnumerable", true)]
    [Arguments("System.Collections.Generic.IEnumerable<int>?", true)]
    public async Task EnumerableTypeSyntaxIsConservativeAsync(string source, bool expected)
    {
        var type = SyntaxFactory.ParseTypeName(source);
        await Assert.That(Psh1125MultipleEnumerationAnalyzer.IsEnumerableTypeSyntax(type)).IsEqualTo(expected);
    }

    /// <summary>Checks alternative execution paths and writes do not count as repeated enumeration.</summary>
    /// <param name="body">The sequence uses in the method body.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("return flag ? source.Count() : source.First();")]
    [Arguments("return flag switch { true => source.Count(), false => source.First() };")]
    [Arguments("try { return source.Count(); } catch { return source.First(); }")]
    [Arguments("return source.GetHashCode();")]
    [Arguments("var deferred = source.Where(x => x > 0); return 0;")]
    [Arguments("IEnumerable<int> unused; return 0;")]
    [Arguments("IEnumerable<int> items = new int[1]; return items.Count() + items.First();")]
    [Arguments("IEnumerable<int> items = new[] { 1 }; return items.Count() + items.First();")]
    [Arguments("IEnumerable<int> items = new List<int>(); return items.Count() + items.First();")]
    [Arguments("IEnumerable<int> items = [1]; return items.Count() + items.First();")]
    [Arguments("IEnumerable<int> items = source.ToArray(); return items.Count() + items.First();")]
    [Arguments("Reset(ref source); return source.Count() + source.First();")]
    [Arguments("Reset(out source, 1); return source.Count() + source.First();")]
    public Task NonRepeatedWalkShapesAreIgnoredAsync(string body) =>
        VerifyAsync($$"""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                int M(IEnumerable<int> source, bool flag) { {{body}} }
                static void Reset(ref IEnumerable<int> value) { }
                static void Reset(out IEnumerable<int> value, int unused) { value = new int[0]; }
            }
            """);

    /// <summary>Checks deferred foreach chains and sequential exception paths still count as repeated walks.</summary>
    /// <param name="body">The sequence uses with the repeated walk marked.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("foreach (var x in source.Where(x => x > 0)) { } foreach (var x in {|PSH1125:source|}.Where(x => x > 1)) { }")]
    [Arguments("try { _ = source.Count(); } finally { _ = {|PSH1125:source|}.Count(); }")]
    [Arguments("if (source.Any()) { _ = {|PSH1125:source|}.First(); }")]
    [Arguments("_ = source.Any() ? {|PSH1125:source|}.First() : 0;")]
    [Arguments("_ = source.Count() switch { _ => {|PSH1125:source|}.First() };")]
    [Arguments("switch (source.Count()) { default: _ = {|PSH1125:source|}.First(); break; }")]
    public Task SequentialWalkShapesAreReportedAsync(string body) =>
        VerifyAsync($$"""
            using System.Collections.Generic;
            using System.Linq;
            class C { void M(IEnumerable<int> source) { {{body}} } }
            """);

    /// <summary>Checks nullable and non-generic sequence contracts are recognized while ref parameters are exempt.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SequenceTypeSyntaxRetainsItsContractAsync() =>
        VerifyAsync("""
            #nullable enable
            class C
            {
                void M(System.Collections.IEnumerable source)
                {
                    foreach (var item in source) { }
                    foreach (var item in {|PSH1125:source|}) { }
                }
                void N(System.Collections.Generic.IEnumerable<int>? source)
                {
                    if (source == null) return;
                    foreach (var item in source) { }
                    foreach (var item in {|PSH1125:source|}) { }
                }
                void R(ref System.Collections.IEnumerable source)
                {
                    foreach (var item in source) { }
                    foreach (var item in source) { }
                }
            }
            """);

    /// <summary>Checks user types named IEnumerable and unrelated eager-looking methods are not mistaken for LINQ.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SimilarTypeAndMethodNamesAreIgnoredAsync() =>
        VerifyAsync("""
            class IEnumerable { public int Count() => 0; }
            class C
            {
                int M(IEnumerable source) => source.Count() + source.Count();
                int N()
                {
                    IEnumerable source = Make();
                    return source.Count() + source.Count();
                }
                IEnumerable Make() => new IEnumerable();
            }
            """);

    /// <summary>Verifies two foreach loops over an IEnumerable parameter are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TwoForEachLoopsAreReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    var total = 0;
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    foreach (var value in {|PSH1125:source|})
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies a Count() followed by a foreach over the same parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CountThenForEachIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    var total = source.Count();
                    foreach (var value in {|PSH1125:source|})
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an Any() guard followed by a First() on the same parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AnyThenFirstIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source) => source.Any() ? {|PSH1125:source|}.First() : 0;
            }
            """);

    /// <summary>Verifies a deferred chain ending in an eager call counts as a walk of its root.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeferredChainEndingInEagerCallIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    var high = source.Where(x => x > 10).Count();
                    var low = {|PSH1125:source|}.Where(x => x < 10).Count();
                    return high + low;
                }
            }
            """);

    /// <summary>Verifies a lazily-initialized IEnumerable local walked twice is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LazyLocalWalkedTwiceIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    IEnumerable<int> filtered = source.Where(x => x > 0);
                    var count = filtered.Count();
                    foreach (var value in {|PSH1125:filtered|})
                    {
                        count += value;
                    }

                    return count;
                }
            }
            """);

    /// <summary>Verifies a materialized collection type is never reported, because re-walking it is safe.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ListParameterIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(List<int> source)
                {
                    var total = source.Count();
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an IReadOnlyCollection parameter is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadOnlyCollectionParameterIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IReadOnlyCollection<int> source)
                {
                    var total = source.Count();
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an array parameter is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArrayParameterIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Linq;

            public class C
            {
                public int M(int[] source)
                {
                    var total = source.Count();
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies a local materialized with ToList is not reported, even when typed as IEnumerable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MaterializedLocalIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    IEnumerable<int> cached = source.ToList();
                    var count = cached.Count();
                    foreach (var value in cached)
                    {
                        count += value;
                    }

                    return count;
                }
            }
            """);

    /// <summary>Verifies a parameter reassigned to a materialized copy is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReassignedParameterIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    source = source.ToList();
                    var total = source.Count();
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies two walks on opposite arms of an if/else are not reported, because only one runs.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WalksOnOppositeIfElseArmsAreNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source, bool flag)
                {
                    if (flag)
                    {
                        return source.Count();
                    }
                    else
                    {
                        return source.Sum();
                    }
                }
            }
            """);

    /// <summary>Verifies two walks in different switch sections are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WalksInDifferentSwitchSectionsAreNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source, int mode)
                {
                    switch (mode)
                    {
                        case 0:
                            return source.Count();
                        default:
                            return source.Sum();
                    }
                }
            }
            """);

    /// <summary>Verifies a single walk is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleWalkIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source) => source.Count();
            }
            """);

    /// <summary>Verifies a deferred-only chain is not treated as a walk.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeferredOnlyChainsAreNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public IEnumerable<int> M(IEnumerable<int> source)
                    => source.Where(x => x > 0).Select(x => x + source.Count());
            }
            """);

    /// <summary>Verifies handing the sequence to another method is not treated as a walk.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PassingToAnotherMethodIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    Consume(source);
                    return source.Count();
                }

                private static void Consume(IEnumerable<int> values)
                {
                }
            }
            """);

    /// <summary>Verifies the same eager call repeated across the operands of one expression is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RepeatedEagerCallInOneExpressionIsReportedAsync() =>
        VerifyAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public bool M(IEnumerable<string> source)
                    => source.Last().Equals("x", StringComparison.Ordinal) || string.IsNullOrWhiteSpace({|PSH1125:source|}.Last());
            }
            """);

    /// <summary>Verifies a constructor parameter walked twice is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructorParameterWalkedTwiceIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly int _total;

                public C(IEnumerable<int> source)
                {
                    if (!source.Any())
                    {
                        return;
                    }

                    _total = {|PSH1125:source|}.Count();
                }
            }
            """);

    /// <summary>Verifies a local function parameter walked twice is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalFunctionParameterWalkedTwiceIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(int[] values)
                {
                    return Inner(values);

                    static int Inner(IEnumerable<int> source)
                    {
                        var first = source.Count();
                        return first + {|PSH1125:source|}.Sum();
                    }
                }
            }
            """);

    /// <summary>Verifies a property accessor body walking a lazy local twice is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PropertyAccessorLocalWalkedTwiceIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly int[] _values = new int[0];

                public int Total
                {
                    get
                    {
                        IEnumerable<int> filtered = _values.Where(x => x > 0);
                        var count = filtered.Count();
                        return count + {|PSH1125:filtered|}.Sum();
                    }
                }
            }
            """);

    /// <summary>Verifies an indexer's index parameter is not reported, because accessors rebind it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IndexerParameterIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int this[IEnumerable<int> source]
                {
                    get
                    {
                        var count = source.Count();
                        return count + source.Sum();
                    }
                }
            }
            """);

    /// <summary>Verifies a lazy local declared inside a local function is reported exactly once.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalFunctionLocalIsReportedOnceAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(int[] values)
                {
                    return Inner();

                    int Inner()
                    {
                        IEnumerable<int> filtered = values.Where(x => x > 0);
                        var count = filtered.Count();
                        return count + {|PSH1125:filtered|}.Sum();
                    }
                }
            }
            """);

    /// <summary>Verifies two lazy parameters walked twice in one member are both reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TwoCandidatesInOneMemberAreBothReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> first, IEnumerable<int> second)
                {
                    var total = first.Count() + second.Count();
                    foreach (var value in {|PSH1125:first|})
                    {
                        total += value;
                    }

                    foreach (var value in {|PSH1125:second|})
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies disqualifying one candidate does not stop the scan for another.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WriteToOneCandidateLeavesTheOtherReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> first, IEnumerable<int> second)
                {
                    first = second;
                    var total = first.Count() + second.Count();
                    foreach (var value in {|PSH1125:second|})
                    {
                        total += value;
                    }

                    return total + first.Count();
                }
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
