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

    /// <summary>Verifies declarations without a partial modifier are outside this analyzer's scope.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonPartialDeclarationsAreIgnoredAsync() =>
        VerifyPartial.VerifyAnalyzerAsync("public class C { public void M() { } } public struct S { } public interface I { } public record R; public record struct RS;");

    /// <summary>Verifies inherited documentation satisfies a partial declaration without further checks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedPartialDocumentationIsAcceptedAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <inheritdoc/>
            public partial class C<T> { }
            """);

    /// <summary>Verifies a type parameter may be documented on either sibling declaration.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SiblingTypeParameterDocumentationIsAcceptedAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public partial class C<T> { }
            /// <content>Additional members.</content>
            /// <typeparam name="T">The item.</typeparam>
            public partial class C<T> { }
            /// <content>More members.</content>
            public partial class C<T> { }
            """);

    /// <summary>Verifies a sibling without documentation cannot supply the missing type parameter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndocumentedSiblingDoesNotSupplyTypeParameterAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public partial class C<{|SST1619:T|}> { }
            public partial class {|SST1601:C|}<T> { }
            """);

    /// <summary>Verifies sibling prose does not replace a missing typeparam element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SiblingSummaryDoesNotSupplyTypeParameterAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public partial class C<{|SST1619:T|}> { }
            /// <content>Additional members.</content>
            public partial class C<{|SST1619:T|}> { }
            """);

    /// <summary>Verifies partial methods accept summaries without type-level parameter checks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DocumentedPartialMethodsAreAcceptedAsync() =>
        VerifyPartial.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public partial class C
            {
                /// <summary>Performs work.</summary>
                partial void M<T>();
            }
            """);

    /// <summary>Verifies documentation scope options include and exclude the corresponding partial declarations.</summary>
    /// <param name="declaration">The declaration with expected diagnostic markup.</param>
    /// <param name="setting">The documentation option.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public partial class {|SST1601:C|} { }", "document_exposed_elements = true")]
    [Arguments("public partial class C { }", "document_exposed_elements = false")]
    [Arguments("internal partial struct {|SST1601:C|} { }", "document_internal_elements = true")]
    [Arguments("internal partial struct C { }", "document_internal_elements = false")]
    [Arguments("class Outer { private partial class {|SST1601:C|} { } }", "document_private_elements = true")]
    [Arguments("class Outer { private partial class C { } }", "document_private_elements = false")]
    [Arguments("internal partial interface {|SST1601:I|} { }", "document_interfaces = all")]
    [Arguments("internal partial interface I { }", "document_interfaces = exposed")]
    [Arguments("public partial interface {|SST1601:I|} { }", "document_interfaces = exposed")]
    [Arguments("public partial interface I { }", "document_interfaces = none")]
    [Arguments("public partial class {|SST1601:C|} { private int value; }", "document_private_fields = true")]
    [Arguments("public partial class {|SST1601:C|} { private int value; }", "document_private_fields = false")]
    public async Task DocumentationOptionsControlPartialScopeAsync(string declaration, string setting)
    {
        var test = new VerifyPartial.Test { TestCode = declaration };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $"root = true\n[*.cs]\nstylesharp.{setting}\n"));
        await test.RunAsync(CancellationToken.None);
    }
}
