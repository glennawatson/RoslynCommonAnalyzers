// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2420IndexOfSkipsFirstAnalyzer,
    StyleSharp.Analyzers.Sst2420IndexOfSkipsFirstCodeFixProvider>;
using VerifyIndexOf = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2420IndexOfSkipsFirstAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2420 (an index-of test that skips the first position).</summary>
public class IndexOfSkipsFirstAnalyzerUnitTest
{
    /// <summary>Verifies a custom search is ignored when the target library provides no generic list interface.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingListInterfaceIsCleanAsync()
    {
        var compilation = CSharpCompilation.Create(
            nameof(MissingListInterfaceIsCleanAsync),
            [CSharpSyntaxTree.ParseText("""
                namespace System
                {
                    public class Object { }
                    public class ValueType { }
                    public struct Int32 { }
                    public struct Boolean { }
                    public struct Void { }
                }
                class Search { public int IndexOf() => 0; }
                class C { bool M(Search search) => search.IndexOf() > 0; }
                """)]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2420IndexOfSkipsFirstAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
        await Assert.That(compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1")).IsNull();
    }

    /// <summary>Verifies searches on recognized static containers and the list interface are reported.</summary>
    /// <param name="expression">The search expression.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("items.IndexOf(1)")]
    [Arguments("System.Array.IndexOf(array, 1)")]
    [Arguments("System.Array.LastIndexOf(array, 1)")]
    [Arguments("System.MemoryExtensions.IndexOf<int>(span, 1)")]
    [Arguments("System.Collections.Immutable.ImmutableArray.IndexOf(1)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RecognizedSearchContainersAreReportedAsync(string expression) =>
        VerifyIndexOf.VerifyAnalyzerAsync($$"""
            class C
            {
                bool M(System.Collections.Generic.IList<int> items, int[] array, System.Span<int> span)
                    => {|SST2420:{{expression}} > 0|};
            }
            namespace System.Collections.Immutable
            {
                static class ImmutableArray { public static int IndexOf(int value) => 0; }
            }
            """);

    /// <summary>Verifies only a direct index-search call compared with the integer literal zero qualifies.</summary>
    /// <param name="expression">The non-reportable comparison.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("s.IndexOf('a') > 1")]
    [Arguments("s.IndexOf('a') > 0L")]
    [Arguments("s.IndexOf('a') > zero")]
    [Arguments("s.IndexOf('a') < 0")]
    [Arguments("(s.IndexOf('a')) > 0")]
    [Arguments("IndexOf() > 0")]
    [Arguments("s.CompareTo(s) > 0")]
    [Arguments("other.IndexOf() > 0")]
    [Arguments("other.LastIndexOf() > 0")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SearchNearMissesAreCleanAsync(string expression) =>
        VerifyIndexOf.VerifyAnalyzerAsync($$"""
            class Other : System.IDisposable
            {
                public int IndexOf() => 0;
                public long LastIndexOf() => 0;
                public void Dispose() { }
            }
            class C
            {
                int IndexOf() => 0;
                bool M(string s, Other other) { const int zero = 0; return {{expression}}; }
            }
            """);

    /// <summary>Verifies an unresolved index-search call is ignored while code is incomplete.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnresolvedIndexSearchIsCleanAsync()
    {
        var test = new VerifyIndexOf.Test
        {
            TestCode = "class C { bool M(string s) => s.IndexOf(new object()) > 0; }",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>A string index-of tested with greater-than-zero, the shape the rule reports.</summary>
    private const string IndexOfGreaterThanZeroSource = """
        public sealed class C
        {
            public bool M(string s) => {|SST2420:s.IndexOf('a') > 0|};
        }
        """;

    /// <summary>Verifies a string index-of tested with greater-than-zero is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StringIndexOfGreaterThanZeroIsReportedAsync() =>
        VerifyIndexOf.VerifyAnalyzerAsync(IndexOfGreaterThanZeroSource);

    /// <summary>Verifies the reversed <c>0 &lt; IndexOf</c> form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ZeroLessThanIndexOfIsReportedAsync() =>
        VerifyIndexOf.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(string s) => {|SST2420:0 < s.IndexOf('a')|};
            }
            """);

    /// <summary>Verifies a list index-of is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ListIndexOfIsReportedAsync() =>
        VerifyIndexOf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public bool M(List<int> items, int x) => {|SST2420:items.IndexOf(x) > 0|};
            }
            """);

    /// <summary>Verifies the correct <c>&gt;= 0</c> form is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GreaterThanOrEqualZeroIsCleanAsync() =>
        VerifyIndexOf.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(string s) => s.IndexOf('a') >= 0;
            }
            """);

    /// <summary>Verifies the deliberate <c>&gt;= 1</c> form is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GreaterThanOrEqualOneIsCleanAsync() =>
        VerifyIndexOf.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(string s) => s.IndexOf('a') >= 1;
            }
            """);

    /// <summary>Verifies the <c>!= -1</c> form is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NotEqualMinusOneIsCleanAsync() =>
        VerifyIndexOf.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(string s) => s.IndexOf('a') != -1;
            }
            """);

    /// <summary>Verifies the fix promotes Contains where the overload exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixPromotesContainsWhereAvailableAsync()
    {
        var test = new VerifyFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = IndexOfGreaterThanZeroSource,
            FixedCode = """
                public sealed class C
                {
                    public bool M(string s) => s.Contains('a');
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the fix degrades to a corrected comparison where Contains is absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixDegradesWhereContainsIsAbsentAsync()
    {
        var test = new VerifyFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = IndexOfGreaterThanZeroSource,
            FixedCode = """
                public sealed class C
                {
                    public bool M(string s) => s.IndexOf('a') >= 0;
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
