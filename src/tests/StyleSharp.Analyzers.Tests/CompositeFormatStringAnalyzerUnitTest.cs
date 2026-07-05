// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCompositeFormatString = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1454CompositeFormatStringAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1454CompositeFormatStringAnalyzer"/>.</summary>
public class CompositeFormatStringAnalyzerUnitTest
{
    /// <summary>Verifies a placeholder beyond the supplied argument count is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PlaceholderWithoutArgumentIsReportedAsync()
        => await VerifyCompositeFormatString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string value) => string.Format({|SST1454:"{1}"|}, value);
            }
            """);

    /// <summary>Verifies escaped braces and satisfied placeholders are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SatisfiedPlaceholdersAreCleanAsync()
        => await VerifyCompositeFormatString.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(string value) => string.Format("{{{0}}}", value);
            }
            """);
}
