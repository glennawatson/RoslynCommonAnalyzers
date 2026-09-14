// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2018RedundantNullCheckBesidePatternAnalyzer,
    StyleSharp.Analyzers.Sst2018RedundantNullCheckBesidePatternCodeFixProvider>;
using VerifyNull = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2018RedundantNullCheckBesidePatternAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2018 (a null check beside an is type pattern).</summary>
public class RedundantNullCheckBesidePatternAnalyzerUnitTest
{
    /// <summary>The and form source.</summary>
    private const string AndSource = """
        public sealed class C
        {
            public bool M(object o) => {|SST2018:o != null && o is string|};
        }
        """;

    /// <summary>The and form after the fix.</summary>
    private const string AndFixed = """
        public sealed class C
        {
            public bool M(object o) => o is string;
        }
        """;

    /// <summary>The or form source.</summary>
    private const string OrSource = """
        public sealed class C
        {
            public bool M(object o) => {|SST2018:o == null || o is not string|};
        }
        """;

    /// <summary>The or form after the fix.</summary>
    private const string OrFixed = """
        public sealed class C
        {
            public bool M(object o) => o is not string;
        }
        """;

    /// <summary>The combinator form source.</summary>
    private const string CombinatorSource = """
        public sealed class C
        {
            public bool M(object o) => {|SST2018:o is not null and string|};
        }
        """;

    /// <summary>The combinator form after the fix.</summary>
    private const string CombinatorFixed = """
        public sealed class C
        {
            public bool M(object o) => o is string;
        }
        """;

    /// <summary>Verifies the classic <c>!= null &amp;&amp; is T</c> form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NotNullAndTypeTestIsReportedAsync() =>
        VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => {|SST2018:o != null && o is string|};
            }
            """);

    /// <summary>Verifies the <c>is not null &amp;&amp; is T</c> form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IsNotNullAndTypeTestIsReportedAsync() =>
        VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => {|SST2018:o is not null && o is string|};
            }
            """);

    /// <summary>Verifies the <c>== null || is not T</c> form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullOrNegatedTypeTestIsReportedAsync() =>
        VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => {|SST2018:o == null || o is not string|};
            }
            """);

    /// <summary>Verifies the combinator form <c>is not null and T</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CombinatorFormIsReportedAsync() =>
        VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => {|SST2018:o is not null and string|};
            }
            """);

    /// <summary>Verifies a genuine "non-null but not a T" check is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonNullButNotTypeIsCleanAsync() =>
        VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => o != null && o is not string;
            }
            """);

    /// <summary>Verifies "null or a T" is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullOrTypeIsCleanAsync() =>
        VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => o == null || o is string;
            }
            """);

    /// <summary>Verifies the fix collapses <c>!= null &amp;&amp; is T</c> to the pattern test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixCollapsesAndFormAsync() =>
        VerifyFix.VerifyCodeFixAsync(
            AndSource,
            AndFixed);

    /// <summary>Verifies the fix collapses <c>== null || is not T</c> to the negated pattern test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixCollapsesOrFormAsync() =>
        VerifyFix.VerifyCodeFixAsync(OrSource, OrFixed);

    /// <summary>Verifies the fix collapses the combinator form to the pattern test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixCollapsesCombinatorFormAsync() =>
        VerifyFix.VerifyCodeFixAsync(CombinatorSource, CombinatorFixed);

    /// <summary>Verifies equivalent null checks retain the positive or negative type test when fixed.</summary>
    /// <param name="expression">The redundant null check and type test.</param>
    /// <param name="replacement">The type test that preserves its result.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("null != o && o is string", "o is string")]
    [Arguments("null == o || o is not string", "o is not string")]
    [Arguments("o is null || o is not string", "o is not string")]
    [Arguments("o is not null && o is string text", "o is string text")]
    [Arguments("o is string and not null", "o is string")]
    [Arguments("o is not null and string text", "o is string text")]
    public Task EquivalentNullCheckFormsAreFixedAsync(string expression, string replacement) =>
        VerifyFix.VerifyCodeFixAsync(
            $$"""
            public class C
            {
                public bool M(object o) => {|SST2018:{{expression}}|};
            }
            """,
            $$"""
            public class C
            {
                public bool M(object o) => {{replacement}};
            }
            """);

    /// <summary>Verifies near-miss null checks, patterns, and unstable receivers remain unchanged.</summary>
    /// <param name="expression">The expression that does not contain a redundant null check.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("o != null && true")]
    [Arguments("o != null && o is null")]
    [Arguments("o != null && o is not null")]
    [Arguments("o != null && o is { }")]
    [Arguments("o != null && o is not 1")]
    [Arguments("o != null && other is string")]
    [Arguments("o != other && o is string")]
    [Arguments("o == other || o is not string")]
    [Arguments("true && o is string")]
    [Arguments("false || o is not string")]
    [Arguments("o is not 1 && o is string")]
    [Arguments("o is 1 || o is not string")]
    [Arguments("o is string && o is string")]
    [Arguments("o is not string || o is not string")]
    [Arguments("Get() != null && Get() is string")]
    [Arguments("o is not null and not string")]
    [Arguments("o is string and not \"\"")]
    [Arguments("o is not 1 and string")]
    [Arguments("o is not null or string")]
    [Arguments("o is { } and not null")]
    [Arguments("o is not null and { }")]
    public Task NonredundantPatternsAreCleanAsync(string expression) =>
        VerifyNull.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public object Get() => new object();
                public bool M(object o, object other) => {{expression}};
            }
            """);
}
