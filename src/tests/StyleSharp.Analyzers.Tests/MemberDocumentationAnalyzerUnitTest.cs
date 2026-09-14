// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.DocumentationPeriodCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the member documentation rules (SST1600/1602/1604/1606/1611/1615/1617/1618/1629).</summary>
public class MemberDocumentationAnalyzerUnitTest
{
    /// <summary>The diagnostic markup for an undocumented method.</summary>
    private const string UndocumentedMethod = "{|SST1600:M|}";

    /// <summary>The path the analyzer config file is added at in the test workspace.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>The editorconfig content that opts private fields into the documentation requirement.</summary>
    private const string DocumentPrivateFieldsEnabledConfig = """
        root = true
        [*.cs]
        stylesharp.document_private_fields = true

        """;

    /// <summary>Verifies incomplete parameter names do not produce documentation diagnostics while typing.</summary>
    /// <param name="declaration">The incomplete documented member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public void M(int) { }")]
    [Arguments("public void M<>() { }")]
    public async Task MissingParameterNamesAreIgnoredAsync(string declaration)
    {
        var source = $$"""
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Runs an action.</summary>
                {{declaration}}
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, new(documentationMode: DocumentationMode.Diagnose));
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        await Assert.That(compilation.GetDiagnostics().Any(static d => d.Id == "CS1001")).IsTrue();
        var diagnostics = await compilation.WithAnalyzers([new MemberDocumentationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies delegate parameters, type parameters, and return values use member documentation rules.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DelegateDocumentationAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>Transforms a value.</summary>
            /// <typeparam name="T">The value type.</typeparam>
            /// <param name="value">The input.</param>
            /// <returns>The output.</returns>
            public delegate T Transform<T>(T value);
            /// <summary>Transforms a value.</summary>
            public delegate T {|SST1615:Missing|}<{|SST1618:T|}>(T {|SST1611:value|});
            /// <summary>Runs an action.</summary>
            public delegate void Action();
            """);

    /// <summary>Verifies a static constructor has no instance-constructor summary convention.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task StaticConstructorSummaryAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Registers shared state.</summary>
                static C() { }
            }
            """);

    /// <summary>Verifies a private constructor must use either accepted constructor prefix.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PrivateConstructorRejectsUnrelatedSummaryAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// {|SST1642:<summary>Creates a container.</summary>|}
                private C() { }
            }
            """);

    /// <summary>Verifies top-level inheritance skips all content checks regardless of element order.</summary>
    /// <param name="inheritance">The inheritance element.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("<inheritdoc/>")]
    [Arguments("<inheritdoc></inheritdoc>")]
    public async Task InheritedDocumentationSkipsContentAsync(string inheritance)
    {
        var source = $$"""
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary/>
                /// <param>Missing name</param>
                /// <returns>No punctuation</returns>
                /// {{inheritance}}
                public void M<T>(T value) { }
            }
            """;
        await Verify.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies a self-closing summary is reported as empty.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SelfClosingSummaryIsEmptyAsync() =>
        Verify.VerifyAnalyzerAsync("/// <summary/>\npublic class {|SST1606:C|} { }");

    /// <summary>Verifies a partial declaration accepts type parameter documentation from a sibling.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SiblingDocumentsTypeParameterAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public partial class C<T> { }
            public partial class C<T> { }
            /// <content>Additional members.</content>
            public partial class C<T> { }
            /// <content>Generic contract.</content>
            /// <typeparam name="T">The stored type.</typeparam>
            public partial class C<T> { }
            """);

    /// <summary>Verifies missing type parameter documentation is still reported on partial declarations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NoSiblingDocumentsTypeParameterAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public partial class C<{|SST1618:T|}> { }
            public partial class C<T> { }
            /// <content>Additional members.</content>
            public partial class C<{|SST1618:T|}> { }
            """);

    /// <summary>Verifies explicit interface implementations do not require duplicate documentation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ExplicitInterfaceMembersInheritCoverageAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A contract.</summary>
            public interface I
            {
                /// <summary>Runs an action.</summary>
                void M();
                /// <summary>Gets a value.</summary>
                int Value { get; }
            }
            /// <summary>An implementation.</summary>
            public class C : I
            {
                void I.M() { }
                int I.Value => 0;
            }
            """);

    /// <summary>Verifies an unrelated summary is rejected for a restricted setter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RestrictedSetterRequiresGetsAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            public class C
            {
                /// {|SST1624:<summary>The stored value.</summary>|}
                public int Value { get; private set; }
            }
            """);

    /// <summary>Verifies accessibility options enable and disable the corresponding declarations and fields.</summary>
    /// <param name="key">The documentation option.</param>
    /// <param name="visibility">The member accessibility.</param>
    /// <param name="enabled">Whether documentation is required.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("document_exposed_elements", "public", true)]
    [Arguments("document_exposed_elements", "public", false)]
    [Arguments("document_internal_elements", "internal", true)]
    [Arguments("document_internal_elements", "internal", false)]
    [Arguments("document_private_elements", "private", true)]
    [Arguments("document_private_elements", "private", false)]
    public async Task AccessibilityOptionControlsMembersAsync(string key, string visibility, bool enabled)
    {
        var test = new Verify.Test
        {
            TestCode = $$"""
                /// <summary>A container.</summary>
                public class C { {{visibility}} void {{(enabled ? UndocumentedMethod : "M")}}() { } }
                """,
        };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, $"root = true\n[*.cs]\nstylesharp.{key} = {enabled}\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies interface modes distinguish public and internal contracts.</summary>
    /// <param name="mode">The interface documentation mode.</param>
    /// <param name="publicRequired">Whether public contracts require documentation.</param>
    /// <param name="internalRequired">Whether internal contracts require documentation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("all", true, true)]
    [Arguments("exposed", true, false)]
    [Arguments("none", false, false)]
    public async Task InterfaceModeControlsContractsAsync(string mode, bool publicRequired, bool internalRequired)
    {
        var publicName = publicRequired ? "{|SST1600:IPublic|}" : "IPublic";
        var internalName = internalRequired ? "{|SST1600:IInternal|}" : "IInternal";
        var test = new Verify.Test
        {
            TestCode = $$"""
                public interface {{publicName}} { void {{(publicRequired ? UndocumentedMethod : "M")}}(); }
                internal interface {{internalName}} { void {{(mode == "none" ? "M" : UndocumentedMethod)}}(); }
                """,
        };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, $"root = true\n[*.cs]\nstylesharp.document_interfaces = {mode}\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a fully documented type produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValidAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A widget.</summary>
            public class Widget { }
            """);

    /// <summary>Verifies an exposed, undocumented type is reported (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MissingDocumentationAsync() =>
        Verify.VerifyAnalyzerAsync("public class {|SST1600:Widget|} { }");

    /// <summary>Verifies an internal type is required by default (internal elements are documented by default), while a private nested type is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InternalRequiredPrivateIgnoredAsync() =>
        Verify.VerifyAnalyzerAsync("internal class {|SST1600:Outer|} { private class Inner { } }");

    /// <summary>Verifies document_internal_elements = false stops an internal type from being required.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalIgnoredWhenDisabledAsync()
    {
        var test = new Verify.Test { TestCode = "internal class Outer { }" };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
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
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.document_interfaces = none

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an undocumented enum member is reported (SST1602).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EnumMemberAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>Colors.</summary>
            public enum Color { {|SST1602:Red|} }
            """);

    /// <summary>Verifies documentation without a summary is reported (SST1604).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MissingSummaryAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <remarks>Notes.</remarks>
            public class {|SST1604:Widget|} { }
            """);

    /// <summary>Verifies an empty summary is reported (SST1606).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptySummaryAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary></summary>
            public class {|SST1606:Widget|} { }
            """);

    /// <summary>Verifies an undocumented parameter is reported (SST1611).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ParameterAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReturnValueAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VoidReturnAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TypeParameterAsync() =>
        Verify.VerifyAnalyzerAsync(
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

    /// <summary>Verifies Fix All adds a terminal period to every reported documentation line in one pass (SST1629).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TerminalPeriodFixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              /// {|SST1629:<summary>A widget</summary>|}
                              public class Widget { }

                              /// {|SST1629:<summary>A gadget</summary>|}
                              public class Gadget { }

                              /// {|SST1629:<summary>A gizmo</summary>|}
                              public class Gizmo { }
                              """;
        const string FixedSource = """
                                   /// <summary>A widget.</summary>
                                   public class Widget { }

                                   /// <summary>A gadget.</summary>
                                   public class Gadget { }

                                   /// <summary>A gizmo.</summary>
                                   public class Gizmo { }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an undocumented private field is not required by default (no <c>document_private_fields</c> set).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateFieldNotRequiredByDefaultAsync() =>
        Verify.VerifyAnalyzerAsync(
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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, DocumentPrivateFieldsEnabledConfig));

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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, DocumentPrivateFieldsEnabledConfig));

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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, DocumentPrivateFieldsEnabledConfig));

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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, DocumentPrivateFieldsEnabledConfig));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a <c>private protected</c> field is not required while <c>document_private_fields</c> is off, even though internal coverage is on.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateProtectedFieldNotRequiredByDefaultAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /// <summary>Outer.</summary>
            public class Outer { private protected int _value; }
            """);

    /// <summary>Verifies an undocumented public field is required by default (<c>document_exposed_elements</c>), independent of the new field option (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PublicFieldRequiredByDefaultAsync() =>
        Verify.VerifyAnalyzerAsync(
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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.document_internal_elements = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an undocumented public event field is required by default (SST1600).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PublicEventFieldRequiredByDefaultAsync() =>
        Verify.VerifyAnalyzerAsync(
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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, DocumentPrivateFieldsEnabledConfig));

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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, DocumentPrivateFieldsEnabledConfig));

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
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, DocumentPrivateFieldsEnabledConfig));

        await test.RunAsync(CancellationToken.None);
    }
}
