// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    [Test]
    public async Task NotNullAndTypeTestIsReportedAsync()
        => await VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => {|SST2018:o != null && o is string|};
            }
            """);

    /// <summary>Verifies the <c>is not null &amp;&amp; is T</c> form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNotNullAndTypeTestIsReportedAsync()
        => await VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => {|SST2018:o is not null && o is string|};
            }
            """);

    /// <summary>Verifies the <c>== null || is not T</c> form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullOrNegatedTypeTestIsReportedAsync()
        => await VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => {|SST2018:o == null || o is not string|};
            }
            """);

    /// <summary>Verifies the combinator form <c>is not null and T</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinatorFormIsReportedAsync()
        => await VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => {|SST2018:o is not null and string|};
            }
            """);

    /// <summary>Verifies a genuine "non-null but not a T" check is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonNullButNotTypeIsCleanAsync()
        => await VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => o != null && o is not string;
            }
            """);

    /// <summary>Verifies "null or a T" is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullOrTypeIsCleanAsync()
        => await VerifyNull.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object o) => o == null || o is string;
            }
            """);

    /// <summary>Verifies the fix collapses <c>!= null &amp;&amp; is T</c> to the pattern test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixCollapsesAndFormAsync()
        => await VerifyFix.VerifyCodeFixAsync(
            AndSource,
            AndFixed);

    /// <summary>Verifies the fix collapses <c>== null || is not T</c> to the negated pattern test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixCollapsesOrFormAsync()
        => await VerifyFix.VerifyCodeFixAsync(OrSource, OrFixed);

    /// <summary>Verifies the fix collapses the combinator form to the pattern test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixCollapsesCombinatorFormAsync()
        => await VerifyFix.VerifyCodeFixAsync(CombinatorSource, CombinatorFixed);
}
