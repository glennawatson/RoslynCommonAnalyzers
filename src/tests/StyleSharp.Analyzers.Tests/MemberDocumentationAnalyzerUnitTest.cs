// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.DocumentationPeriodCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the member documentation rules (SST1600/1602/1604/1606/1611/1615/1617/1618/1629).</summary>
public class MemberDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies a fully documented type produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A widget.</summary>
            public class Widget { }
            """);

    /// <summary>Verifies an exposed, undocumented type is reported (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingDocumentationAsync()
        => await Verify.VerifyAnalyzerAsync("public class {|SST1600:Widget|} { }");

    /// <summary>Verifies non-exposed members are not required to be documented.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonExposedIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync("internal class Outer { private class Inner { } }");

    /// <summary>Verifies an undocumented enum member is reported (SST1602).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumMemberAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>Colors.</summary>
            public enum Color { {|SST1602:Red|} }
            """);

    /// <summary>Verifies documentation without a summary is reported (SST1604).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingSummaryAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <remarks>Notes.</remarks>
            public class {|SST1604:Widget|} { }
            """);

    /// <summary>Verifies an empty summary is reported (SST1606).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptySummaryAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary></summary>
            public class {|SST1606:Widget|} { }
            """);

    /// <summary>Verifies an undocumented parameter is reported (SST1611).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                public void M(int {|SST1611:value|}) { }
            }
            """);

    /// <summary>Verifies a missing return value is reported (SST1615).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnValueAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets a value.</summary>
                public int {|SST1615:M|}() => 0;
            }
            """);

    /// <summary>Verifies a documented void return is reported (SST1617).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VoidReturnAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                /// <returns>Nothing.</returns>
                public void {|SST1617:M|}() { }
            }
            """);

    /// <summary>Verifies an undocumented type parameter is reported (SST1618).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                public void M<{|SST1618:T|}>() { }
            }
            """);

    /// <summary>Verifies summary text without terminal punctuation is reported and a period is added (SST1629).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TerminalPeriodAsync()
    {
        const string Source = """
                              /// {|SST1629:<summary>A widget</summary>|}
                              public class Widget { }
                              """;
        const string FixedSource = """
                                   /// <summary>A widget.</summary>
                                   public class Widget { }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
