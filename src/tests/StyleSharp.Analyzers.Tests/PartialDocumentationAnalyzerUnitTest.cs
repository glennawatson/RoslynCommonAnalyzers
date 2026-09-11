// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyMember = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer>;
using VerifyPartial = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.PartialDocumentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the partial-element documentation rules (SST1601/SST1605/SST1607/SST1619).</summary>
public class PartialDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies an undocumented exposed partial type is reported (SST1601).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndocumentedPartialReportedAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class {|SST1601:C|}
            {
            }
            """);

    /// <summary>Verifies a documented partial without a summary is reported (SST1605).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialWithoutSummaryReportedAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <remarks>Notes.</remarks>
            public partial class {|SST1605:C|}
            {
            }
            """);

    /// <summary>Verifies a partial documented with a content element is accepted (SST1605).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// One part says what the type is; the rest say what they add. Demanding the summary in every part
    /// would state the same thing several times and leave the reader to pick the authoritative copy.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialDocumentedWithContentIsCleanAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <content>The parsing members.</content>
            public partial class C
            {
            }
            """);

    /// <summary>Verifies an empty content element does not stand in for the summary (SST1605).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialWithEmptyContentIsReportedAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <content></content>
            public partial class {|SST1605:C|}
            {
            }
            """);

    /// <summary>Verifies a content element on a type that is not partial does not stand in for the summary.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>There is no other part to carry the summary, so the type would be left undescribed.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ContentOnANonPartialTypeIsStillReportedAsync() =>
        VerifyMember.VerifyAnalyzerAsync(
            """
            /// <content>The parsing members.</content>
            public class {|SST1604:C|}
            {
            }
            """);

    /// <summary>Verifies a documented partial with an empty summary is reported (SST1607).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialWithEmptySummaryReportedAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <summary></summary>
            public partial class {|SST1607:C|}
            {
            }
            """);

    /// <summary>Verifies an undocumented partial type parameter is reported (SST1619).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialTypeParameterUndocumentedReportedAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public partial class C<{|SST1619:T|}>
            {
            }
            """);
}
