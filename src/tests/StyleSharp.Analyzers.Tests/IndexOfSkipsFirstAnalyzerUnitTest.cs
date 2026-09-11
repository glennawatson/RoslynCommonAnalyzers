// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2420IndexOfSkipsFirstAnalyzer,
    StyleSharp.Analyzers.Sst2420IndexOfSkipsFirstCodeFixProvider>;
using VerifyIndexOf = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2420IndexOfSkipsFirstAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2420 (an index-of test that skips the first position).</summary>
public class IndexOfSkipsFirstAnalyzerUnitTest
{
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
