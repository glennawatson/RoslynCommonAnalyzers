// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests documentation scope after declared and containing accessibilities are combined.</summary>
public sealed class DocumentationVisibilityTests
{
    /// <summary>The scope for public and protected declarations.</summary>
    private const string ExposedScope = "exposed";

    /// <summary>The scope for declarations visible only within the assembly.</summary>
    private const string InternalScope = "internal";

    /// <summary>Member scope follows the narrowest container and each documentation toggle independently.</summary>
    /// <param name="source">The declaration and its containers.</param>
    /// <param name="kind">The declaration to inspect.</param>
    /// <param name="bucket">The expected exposed, internal, or private scope.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public class C { public void M() { } }", SyntaxKind.MethodDeclaration, "exposed")]
    [Arguments("public class C { protected void M() { } }", SyntaxKind.MethodDeclaration, "exposed")]
    [Arguments("public class C { protected internal void M() { } }", SyntaxKind.MethodDeclaration, "exposed")]
    [Arguments("public class C { private protected void M() { } }", SyntaxKind.MethodDeclaration, "internal")]
    [Arguments("public class C { internal void M() { } }", SyntaxKind.MethodDeclaration, "internal")]
    [Arguments("public class C { private void M() { } }", SyntaxKind.MethodDeclaration, "private")]
    [Arguments("public class C { void M() { } }", SyntaxKind.MethodDeclaration, "private")]
    [Arguments("class C { public void M() { } }", SyntaxKind.MethodDeclaration, "internal")]
    [Arguments("namespace N { class C { public void M() { } } }", SyntaxKind.MethodDeclaration, "internal")]
    [Arguments("public class C { private class D { public void M() { } } }", SyntaxKind.MethodDeclaration, "private")]
    [Arguments("public enum E { Value }", SyntaxKind.EnumMemberDeclaration, "exposed")]
    [Arguments("enum E { Value }", SyntaxKind.EnumMemberDeclaration, "internal")]
    [Arguments("public static class C { extension(string s) { public int M() => 0; } }", SyntaxKind.MethodDeclaration, "private")]
    public async Task MembersUseEffectiveAccessibilityAsync(string source, SyntaxKind kind, string bucket)
    {
        var member = SyntaxFactory.ParseCompilationUnit(source).DescendantNodes().Last(node => node.IsKind(kind));
        await AssertMemberScopesAsync(member, bucket);
    }

    /// <summary>Detached declarations and nodes without accessibility terminate the container walk safely.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedMembersUseTheirOwnAccessibilityAsync()
    {
        await AssertMemberScopesAsync(SyntaxFactory.ParseMemberDeclaration("public class C { }")!, ExposedScope);
        await AssertMemberScopesAsync(SyntaxFactory.ClassDeclaration("C"), InternalScope);
        await AssertMemberScopesAsync(SyntaxFactory.IdentifierName("value"), "private");
    }

    /// <summary>Interface declarations use interface mode independently of containing types and other toggles.</summary>
    /// <param name="source">The interface declaration.</param>
    /// <param name="exposed">Whether exposed interface mode includes it.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public interface I { }", true)]
    [Arguments("internal interface I { }", false)]
    [Arguments("interface I { }", false)]
    [Arguments("class C { public interface I { } }", true)]
    [Arguments("public class C { private interface I { } }", true)]
    public async Task InterfaceDeclarationsFollowInterfaceModeAsync(string source, bool exposed)
    {
        var member = SyntaxFactory.ParseCompilationUnit(source).DescendantNodes().OfType<InterfaceDeclarationSyntax>().Single();
        var all = new DocumentationCoverage(false, false, false, false, DocumentationInterfaceMode.All);
        var exposedOnly = all with { Interfaces = DocumentationInterfaceMode.Exposed };
        var none = new DocumentationCoverage(true, true, true, true, DocumentationInterfaceMode.None);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, all)).IsTrue();
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, exposedOnly)).IsEqualTo(exposed);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none)).IsFalse();
    }

    /// <summary>Public interface members use interface mode; non-public members use the private-elements toggle.</summary>
    /// <param name="declaration">The interface member.</param>
    /// <param name="isPublic">Whether interface mode governs the member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void M();", true)]
    [Arguments("public void M();", true)]
    [Arguments("private void M() { }", false)]
    [Arguments("internal void M() { }", false)]
    [Arguments("protected void M() { }", false)]
    public async Task InterfaceMembersChooseTheirOwnToggleAsync(string declaration, bool isPublic)
    {
        var member = SyntaxFactory.ParseCompilationUnit($"public interface I {{ {declaration} }}")
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var none = new DocumentationCoverage(false, false, false, false, DocumentationInterfaceMode.None);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none)).IsFalse();
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none with { Interfaces = DocumentationInterfaceMode.All })).IsEqualTo(isPublic);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none with { Interfaces = DocumentationInterfaceMode.Exposed })).IsEqualTo(isPublic);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none with { PrivateElements = true })).IsEqualTo(!isPublic);
    }

    /// <summary>Direct interface children without accessibility still follow interface mode.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InterfaceChildWithoutAccessibilityUsesInterfaceModeAsync()
    {
        var node = SyntaxFactory.ParseCompilationUnit("public interface I<T> { }")
            .DescendantNodes().OfType<TypeParameterListSyntax>().Single();
        var none = new DocumentationCoverage(false, false, false, false, DocumentationInterfaceMode.None);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(node, none)).IsFalse();
        await Assert.That(DocumentationVisibility.NeedsDocumentation(node, none with { Interfaces = DocumentationInterfaceMode.All })).IsTrue();
        await Assert.That(DocumentationVisibility.NeedsDocumentation(node, none with { PrivateElements = true })).IsFalse();
    }

    /// <summary>Field scope uses its separate private toggle, including private protected and restricted containers.</summary>
    /// <param name="source">The field and its containers.</param>
    /// <param name="bucket">The expected documentation scope.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public class C { public int value; }", "exposed")]
    [Arguments("public class C { protected int value; }", "exposed")]
    [Arguments("public class C { protected internal int value; }", "exposed")]
    [Arguments("public class C { internal int value; }", "internal")]
    [Arguments("public class C { private protected int value; }", "private")]
    [Arguments("public class C { private int value; }", "private")]
    [Arguments("public class C { int value; }", "private")]
    [Arguments("class C { public int value; }", "internal")]
    [Arguments("namespace N { class C { public int value; } }", "internal")]
    [Arguments("public class C { private class D { public int value; } }", "private")]
    [Arguments("public class C { public event System.Action Changed; }", "exposed")]
    [Arguments("public static class C { extension(string s) { public int value; } }", "private")]
    public async Task FieldsUseSeparatePrivateScopeAsync(string source, string bucket)
    {
        var field = SyntaxFactory.ParseCompilationUnit(source).DescendantNodes().OfType<BaseFieldDeclarationSyntax>().Single();
        await AssertFieldScopesAsync(field, bucket);
    }

    /// <summary>A field without parents still applies its declared scope.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedFieldsUseDeclaredAccessibilityAsync()
    {
        var field = (BaseFieldDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("public int value;")!;
        await AssertFieldScopesAsync(field, ExposedScope);
    }

    /// <summary>Extension blocks inherit the container scope and honor private documentation independently.</summary>
    /// <param name="modifiers">The containing class accessibility.</param>
    /// <param name="bucket">The expected effective scope.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public", "exposed")]
    [Arguments("internal", "internal")]
    [Arguments("private", "private")]
    public async Task ContainerDocumentationUsesContainingTypeAsync(string modifiers, string bucket)
    {
        var root = SyntaxFactory.ParseCompilationUnit($"public class Outer {{ {modifiers} static class C {{ extension(string s) {{ }} }} }}");
        var node = root.DescendantNodes().OfType<ExtensionBlockDeclarationSyntax>().Single();
        var none = new DocumentationCoverage(false, false, false, false, DocumentationInterfaceMode.None);
        await Assert.That(DocumentationVisibility.NeedsContainerDocumentation(node, none)).IsFalse();
        await Assert.That(DocumentationVisibility.NeedsContainerDocumentation(node, none with { ExposedElements = true })).IsEqualTo(bucket == ExposedScope);
        await Assert.That(DocumentationVisibility.NeedsContainerDocumentation(node, none with { InternalElements = true })).IsEqualTo(bucket == InternalScope);
        await Assert.That(DocumentationVisibility.NeedsContainerDocumentation(node, none with { PrivateElements = true })).IsTrue();
    }

    /// <summary>An uncontained node has no effective visibility to document.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UncontainedNodeHasNoDocumentationScopeAsync()
    {
        var coverage = new DocumentationCoverage(true, true, false, false, DocumentationInterfaceMode.All);
        await Assert.That(DocumentationVisibility.NeedsContainerDocumentation(SyntaxFactory.IdentifierName("value"), coverage)).IsFalse();
    }

    /// <summary>Checks each independent element scope against the expected effective bucket.</summary>
    /// <param name="member">The declaration to check.</param>
    /// <param name="bucket">The expected scope.</param>
    /// <returns>A task representing the asynchronous assertions.</returns>
    private static async Task AssertMemberScopesAsync(SyntaxNode member, string bucket)
    {
        var none = new DocumentationCoverage(false, false, false, false, DocumentationInterfaceMode.None);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none)).IsFalse();
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none with { ExposedElements = true })).IsEqualTo(bucket == ExposedScope);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none with { InternalElements = true })).IsEqualTo(bucket == InternalScope);
        await Assert.That(DocumentationVisibility.NeedsDocumentation(member, none with { PrivateElements = true })).IsTrue();
    }

    /// <summary>Checks every field scope and ensures private-elements does not document private fields.</summary>
    /// <param name="field">The field to check.</param>
    /// <param name="bucket">The expected scope.</param>
    /// <returns>A task representing the asynchronous assertions.</returns>
    private static async Task AssertFieldScopesAsync(BaseFieldDeclarationSyntax field, string bucket)
    {
        var none = new DocumentationCoverage(false, false, false, false, DocumentationInterfaceMode.None);
        await Assert.That(DocumentationVisibility.FieldNeedsDocumentation(field, none)).IsFalse();
        await Assert.That(DocumentationVisibility.FieldNeedsDocumentation(field, none with { ExposedElements = true })).IsEqualTo(bucket == ExposedScope);
        await Assert.That(DocumentationVisibility.FieldNeedsDocumentation(field, none with { InternalElements = true })).IsEqualTo(bucket == InternalScope);
        await Assert.That(DocumentationVisibility.FieldNeedsDocumentation(field, none with { PrivateFields = true })).IsEqualTo(bucket == "private");
        await Assert.That(DocumentationVisibility.FieldNeedsDocumentation(field, none with { PrivateElements = true })).IsFalse();
    }
}
