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

    /// <summary>Verifies an internal type is required by default (internal elements are documented by default), while a private nested type is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalRequiredPrivateIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync("internal class {|SST1600:Outer|} { private class Inner { } }");

    /// <summary>Verifies document_internal_elements = false stops an internal type from being required.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalIgnoredWhenDisabledAsync()
    {
        var test = new Verify.Test { TestCode = "internal class Outer { }" };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_internal_elements = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies document_private_elements = true makes a private nested type required.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateRequiredWhenEnabledAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer { private class {|SST1600:Inner|} { } }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_elements = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies document_interfaces = none stops an interface and its members from being required.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfacesNoneIgnoresInterfaceAsync()
    {
        var test = new Verify.Test { TestCode = "public interface IThing { void Do(); }" };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_interfaces = none

            """));

        await test.RunAsync(CancellationToken.None);
    }

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

    /// <summary>Verifies an undocumented private field is not required by default (no <c>document_private_fields</c> set).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateFieldNotRequiredByDefaultAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>Outer.</summary>
            public class Outer { private int _value; }
            """);

    /// <summary>Verifies an undocumented private field is not required when <c>document_private_fields = false</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateFieldNotRequiredWhenDisabledAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer { private int _value; }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_fields = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an undocumented private field is reported when <c>document_private_fields = true</c> (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateFieldRequiredWhenEnabledAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer { private int {|SST1600:_value|}; }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_fields = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a documented private field is accepted when <c>document_private_fields = true</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateFieldDocumentedWhenEnabledAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer
                       {
                           /// <summary>The value.</summary>
                           private int _value;
                       }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_fields = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a private const field follows <c>document_private_fields</c>, like any other private field (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateConstFieldRequiredWhenEnabledAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer { private const int {|SST1600:Value|} = 1; }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_fields = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a <c>private protected</c> field is treated as private and follows <c>document_private_fields</c> (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateProtectedFieldTreatedAsPrivateAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer { private protected int {|SST1600:_value|}; }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_fields = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a <c>private protected</c> field is not required while <c>document_private_fields</c> is off, even though internal coverage is on.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateProtectedFieldNotRequiredByDefaultAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>Outer.</summary>
            public class Outer { private protected int _value; }
            """);

    /// <summary>Verifies an undocumented public field is required by default (<c>document_exposed_elements</c>), independent of the new field option (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicFieldRequiredByDefaultAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>Outer.</summary>
            public class Outer { public int {|SST1600:Value|}; }
            """);

    /// <summary>Verifies an undocumented internal field is no longer required once <c>document_internal_elements = false</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalFieldGovernedByInternalKeyAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer { internal int Value; }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_internal_elements = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an undocumented public event field is required by default (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicEventFieldRequiredByDefaultAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            /// <summary>Outer.</summary>
            public class Outer { public event EventHandler {|SST1600:Changed|}; }
            """);

    /// <summary>Verifies a documented auto-property's compiler-generated backing field is never flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AutoPropertyBackingFieldNotFlaggedAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer
                       {
                           /// <summary>Gets or sets the value.</summary>
                           public int Value { get; set; }
                       }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_fields = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a <c>[GeneratedCode]</c> private field is never flagged, even with <c>document_private_fields = true</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedCodeFieldNotFlaggedAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       using System.CodeDom.Compiler;

                       /// <summary>Outer.</summary>
                       public class Outer
                       {
                           [GeneratedCode("tool", "1.0")]
                           private int _value;
                       }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_fields = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies each declarator of a multi-declarator private field is reported separately (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiDeclaratorPrivateFieldAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       /// <summary>Outer.</summary>
                       public class Outer { private int {|SST1600:_a|}, {|SST1600:_b|}; }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.document_private_fields = true

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
