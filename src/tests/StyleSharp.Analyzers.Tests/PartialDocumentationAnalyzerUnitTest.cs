// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPartial = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.PartialDocumentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the partial-element documentation rules (SST1601/SST1605/SST1607/SST1619).</summary>
public class PartialDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies an undocumented exposed partial type is reported (SST1601).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UndocumentedPartialReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class {|SST1601:C|}
            {
            }
            """);

    /// <summary>Verifies a documented partial without a summary is reported (SST1605).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialWithoutSummaryReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <remarks>Notes.</remarks>
            public partial class {|SST1605:C|}
            {
            }
            """);

    /// <summary>Verifies a documented partial with an empty summary is reported (SST1607).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialWithEmptySummaryReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <summary></summary>
            public partial class {|SST1607:C|}
            {
            }
            """);

    /// <summary>Verifies an undocumented partial type parameter is reported (SST1619).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTypeParameterUndocumentedReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public partial class C<{|SST1619:T|}>
            {
            }
            """);
}
