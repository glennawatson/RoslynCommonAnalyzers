// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUnnecessaryParentheses = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1459UnnecessaryParenthesesAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1459UnnecessaryParenthesesAnalyzer"/>.</summary>
public class UnnecessaryParenthesesAnalyzerUnitTest
{
    /// <summary>Verifies a return value wrapped in non-grouping parentheses is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StandaloneReturnValueIsReportedAsync()
        => await VerifyUnnecessaryParentheses.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value) => {|SST1459:(value)|};
            }
            """);

    /// <summary>Verifies parentheses that declare precedence are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PrecedenceParenthesesAreCleanAsync()
        => await VerifyUnnecessaryParentheses.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int left, int right) => (left + right) * 2;
            }
            """);
}
