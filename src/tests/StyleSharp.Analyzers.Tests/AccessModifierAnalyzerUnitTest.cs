// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Helper-level tests for access-modifier analysis fast paths.</summary>
public sealed class AccessModifierAnalyzerUnitTest
{
    /// <summary>Verifies the access-modifier scan recognizes ordinary accessibility keywords.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasAccessModifierRecognizesAccessibilityKeywordsAsync()
    {
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        await Assert.That(AccessModifierAnalyzer.HasAccessModifierFast(modifiers)).IsTrue();
    }

    /// <summary>Verifies the access-modifier scan recognizes the file modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasAccessModifierRecognizesFileModifierAsync()
    {
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.FileKeyword));

        await Assert.That(AccessModifierAnalyzer.HasAccessModifierFast(modifiers)).IsTrue();
    }

    /// <summary>Verifies the access-modifier scan stays false when no accessibility modifier is present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasAccessModifierSkipsNonAccessibilityModifiersAsync()
    {
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword));

        await Assert.That(AccessModifierAnalyzer.HasAccessModifierFast(modifiers)).IsFalse();
    }

    /// <summary>Verifies static constructors do not require an explicit access modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequiresModifierSkipsStaticConstructorsAsync()
    {
        var constructor = ParseMember<ConstructorDeclarationSyntax>("class C { static C() { } }", SyntaxKind.ConstructorDeclaration);

        await Assert.That(AccessModifierAnalyzer.RequiresModifierFast(constructor)).IsFalse();
    }

    /// <summary>Verifies ordinary methods still require an explicit access modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequiresModifierKeepsOrdinaryMethodsAsync()
    {
        var method = ParseMember<MethodDeclarationSyntax>("class C { void M() { } }", SyntaxKind.MethodDeclaration);

        await Assert.That(AccessModifierAnalyzer.RequiresModifierFast(method)).IsTrue();
    }

    /// <summary>Verifies top-level types reuse the cached internal-modifier diagnostic properties.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModifierPropertiesReuseInternalCacheAsync()
    {
        var type = ParseMember<ClassDeclarationSyntax>("class C { }", SyntaxKind.ClassDeclaration);
        var first = AccessModifierAnalyzer.ModifierProperties(type);
        var second = AccessModifierAnalyzer.ModifierProperties(type);

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    /// <summary>Verifies nested members reuse the cached private-modifier diagnostic properties.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModifierPropertiesReusePrivateCacheAsync()
    {
        var method = ParseMember<MethodDeclarationSyntax>("class C { void M() { } }", SyntaxKind.MethodDeclaration);
        var first = AccessModifierAnalyzer.ModifierProperties(method);
        var second = AccessModifierAnalyzer.ModifierProperties(method);

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    /// <summary>Verifies top-level declarations are recognized for internal modifier insertion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TopLevelDeclarationHelperRecognizesCompilationUnitMembersAsync()
    {
        var type = ParseMember<ClassDeclarationSyntax>("class C { }", SyntaxKind.ClassDeclaration);

        await Assert.That(AccessModifierAnalyzer.IsTopLevelDeclaration(type)).IsTrue();
    }

    /// <summary>Verifies nested declarations are not treated as top-level.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TopLevelDeclarationHelperRejectsNestedMembersAsync()
    {
        var method = ParseMember<MethodDeclarationSyntax>("class C { void M() { } }", SyntaxKind.MethodDeclaration);

        await Assert.That(AccessModifierAnalyzer.IsTopLevelDeclaration(method)).IsFalse();
    }

    /// <summary>Parses a single member declaration from a compilation unit or container type.</summary>
    /// <typeparam name="TMember">The member declaration type.</typeparam>
    /// <param name="source">The source containing the target member.</param>
    /// <param name="kind">The syntax kind to select.</param>
    /// <returns>The parsed member declaration.</returns>
    private static TMember ParseMember<TMember>(string source, SyntaxKind kind)
        where TMember : MemberDeclarationSyntax
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);
        foreach (var node in root.DescendantNodes())
        {
            if (node is TMember member && member.Kind() == kind)
            {
                return member;
            }
        }

        throw new InvalidOperationException("Member not found.");
    }
}
