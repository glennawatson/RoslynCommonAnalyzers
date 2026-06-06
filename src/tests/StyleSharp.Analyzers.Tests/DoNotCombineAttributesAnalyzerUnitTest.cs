// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCombineAttributes = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DoNotCombineAttributesAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the do-not-combine-attributes rule (SST1133).</summary>
public class DoNotCombineAttributesAnalyzerUnitTest
{
    /// <summary>Verifies each attribute beyond the first in a combined list is reported (SST1133).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinedAttributesReportedAsync()
        => await VerifyCombineAttributes.VerifyAnalyzerAsync(
            """
            using System;

            [Serializable, {|SST1133:Obsolete|}]
            internal class C
            {
            }
            """);

    /// <summary>Verifies separate attribute lists are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateAttributesAreCleanAsync()
        => await VerifyCombineAttributes.VerifyAnalyzerAsync(
            """
            using System;

            [Serializable]
            [Obsolete]
            internal class C
            {
            }
            """);
}
