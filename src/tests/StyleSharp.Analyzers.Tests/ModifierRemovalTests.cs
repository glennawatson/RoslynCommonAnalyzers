// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests removing one modifier from a member declaration while keeping its layout.</summary>
public class ModifierRemovalTests
{
    /// <summary>Verifies each declaration shape keeps its indentation when a modifier is removed by kind.</summary>
    /// <param name="source">The source holding one declaration with the modifier.</param>
    /// <param name="modifier">The modifier keyword to remove.</param>
    /// <param name="expected">The source after removal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C {\n    public static void M() { }\n}", "static", "class C {\n    public void M() { }\n}")]
    [Arguments("class C {\n    static void M() { }\n}", "static", "class C {\n    void M() { }\n}")]
    [Arguments("class C {\n    public static int F;\n}", "public", "class C {\n    static int F;\n}")]
    [Arguments("class C {\n    [A] public void M() { }\n}", "public", "class C {\n    [A] void M() { }\n}")]
    [Arguments("class C {\n    public C() { }\n}", "public", "class C {\n    C() { }\n}")]
    [Arguments("class C {\n    public int F;\n}", "public", "class C {\n    int F;\n}")]
    [Arguments("class C {\n    public int P { get; }\n}", "public", "class C {\n    int P { get; }\n}")]
    [Arguments("class C {\n    public int this[int i] => i;\n}", "public", "class C {\n    int this[int i] => i;\n}")]
    [Arguments("class C {\n    public event System.Action E { add { } remove { } }\n}", "public", "class C {\n    event System.Action E { add { } remove { } }\n}")]
    [Arguments("class C {\n    public event System.Action E;\n}", "public", "class C {\n    event System.Action E;\n}")]
    [Arguments("class C {\n    public delegate void D();\n}", "public", "class C {\n    delegate void D();\n}")]
    [Arguments("// c\npublic class D { }", "public", "// c\nclass D { }")]
    [Arguments("// c\npublic struct D { }", "public", "// c\nstruct D { }")]
    [Arguments("// c\npublic interface D { }", "public", "// c\ninterface D { }")]
    [Arguments("// c\npublic record D;", "public", "// c\nrecord D;")]
    public async Task RemoveKindKeepsIndentationAsync(string source, string modifier, string expected)
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);
        var kind = SyntaxFacts.GetKeywordKind(modifier);
        var declaration = root.DescendantNodes().OfType<MemberDeclarationSyntax>().First(member => member.Modifiers.Any(kind));

        var updated = root.ReplaceNode(declaration, ModifierRemoval.RemoveKind(declaration, kind));

        await Assert.That(updated.ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies a modifier the declaration does not carry leaves the same instance.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingModifierReturnsTheDeclarationAsync()
    {
        var declaration = SyntaxFactory.ParseMemberDeclaration("public void M() { }")!;

        await Assert.That(ModifierRemoval.RemoveKind(declaration, SyntaxKind.StaticKeyword)).IsSameReferenceAs(declaration);
        await Assert.That(ModifierRemoval.RemoveAt(declaration, -1)).IsSameReferenceAs(declaration);
    }
}
