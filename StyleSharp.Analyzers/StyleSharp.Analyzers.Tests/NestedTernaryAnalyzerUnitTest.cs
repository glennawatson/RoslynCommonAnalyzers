// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNested = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.NestedTernaryAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1147 (do not nest conditional operators).</summary>
public class NestedTernaryAnalyzerUnitTest
{
    /// <summary>Verifies the inner conditional expression is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedConditionalIsReportedAsync()
        => await VerifyNested.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string M(int n) => n == 0 ? "zero" : {|SST1147:n > 0 ? "positive" : "negative"|};
            }
            """);

    /// <summary>Verifies independent conditionals and conditionals in switch arms are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonNestedConditionalsAreCleanAsync()
        => await VerifyNested.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string M(int n) => n == 0 ? "zero" : "other";

                public string N(int n) => n switch
                {
                    0 => n > 1 ? "a" : "b",
                    _ => "c",
                };
            }
            """);
}
