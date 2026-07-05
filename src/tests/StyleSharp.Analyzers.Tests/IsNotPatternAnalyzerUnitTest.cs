// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyIsNotPattern = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2008IsNotPatternAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2008IsNotPatternAnalyzer"/>.</summary>
public class IsNotPatternAnalyzerUnitTest
{
    /// <summary>Verifies a negated null pattern is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegatedPatternIsReportedAsync()
        => await VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value) => {|SST2008:!(value is null)|};
            }
            """);

    /// <summary>Verifies a declaration pattern is skipped because the declared name cannot be preserved.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegatedDeclarationPatternIsCleanAsync()
        => await VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value) => !(value is string text);
            }
            """);
}
