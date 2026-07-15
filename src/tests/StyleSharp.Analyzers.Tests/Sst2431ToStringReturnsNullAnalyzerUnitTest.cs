// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyToString = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2431ToStringReturnsNullAnalyzer,
    StyleSharp.Analyzers.Sst2431ToStringReturnsNullCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2431 (a ToString override that can return null).</summary>
public class Sst2431ToStringReturnsNullAnalyzerUnitTest
{
    /// <summary>An expression body of <c>null!</c>.</summary>
    private const string ExpressionBodyNullBangSource = """
        public sealed class Money
        {
            public override string ToString() => {|SST2431:null!|};
        }
        """;

    /// <summary>The <c>null!</c> expression body after the fix.</summary>
    private const string ExpressionBodyNullBangFixed = """
        public sealed class Money
        {
            public override string ToString() => string.Empty;
        }
        """;

    /// <summary>A <c>return null;</c> statement.</summary>
    private const string ReturnNullSource = """
        public sealed class Money
        {
            public override string ToString()
            {
                return {|SST2431:null|};
            }
        }
        """;

    /// <summary>The <c>return null;</c> statement after the fix.</summary>
    private const string ReturnNullFixed = """
        public sealed class Money
        {
            public override string ToString()
            {
                return string.Empty;
            }
        }
        """;

    /// <summary>A conditional whose false branch is null.</summary>
    private const string ConditionalBranchSource = """
        public sealed class Money
        {
            public bool IsEmpty { get; set; }

            public override string ToString() => IsEmpty ? "x" : {|SST2431:null|};
        }
        """;

    /// <summary>The conditional after the fix.</summary>
    private const string ConditionalBranchFixed = """
        public sealed class Money
        {
            public bool IsEmpty { get; set; }

            public override string ToString() => IsEmpty ? "x" : string.Empty;
        }
        """;

    /// <summary>Verifies <c>=&gt; null!</c> is reported and replaced with <c>string.Empty</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodyNullBangIsFixedAsync()
        => await VerifyToString.VerifyCodeFixAsync(ExpressionBodyNullBangSource, ExpressionBodyNullBangFixed);

    /// <summary>Verifies <c>return null;</c> is reported and replaced with <c>string.Empty</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnNullStatementIsFixedAsync()
        => await VerifyToString.VerifyCodeFixAsync(ReturnNullSource, ReturnNullFixed);

    /// <summary>Verifies a null branch of a conditional is reported and replaced with <c>string.Empty</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalNullBranchIsFixedAsync()
        => await VerifyToString.VerifyCodeFixAsync(ConditionalBranchSource, ConditionalBranchFixed);

    /// <summary>Verifies a non-null literal return is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonNullLiteralIsCleanAsync()
        => await VerifyToString.VerifyAnalyzerAsync(
            """
            public sealed class Money
            {
                public override string ToString() => "x";
            }
            """);

    /// <summary>Verifies returning <c>string.Empty</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringEmptyIsCleanAsync()
        => await VerifyToString.VerifyAnalyzerAsync(
            """
            public sealed class Money
            {
                public override string ToString() => string.Empty;
            }
            """);

    /// <summary>Verifies a null returned by a nested lambda, not by ToString, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullFromNestedLambdaIsCleanAsync()
        => await VerifyToString.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Money
            {
                public override string ToString()
                {
                    Func<string> factory = () => null;
                    return factory() ?? "x";
                }
            }
            """);

    /// <summary>Verifies a non-override method named ToString is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonOverrideNamedToStringIsCleanAsync()
        => await VerifyToString.VerifyAnalyzerAsync(
            """
            public sealed class Money
            {
                public string ToString(int radix) => null;
            }
            """);
}
