// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the single-update rebuild of class, struct, interface and record declarations.</summary>
public class TypeDeclarationRewriteTests
{
    /// <summary>The parse options that recognise every declaration shape the tests use.</summary>
    private static readonly CSharpParseOptions PreviewOptions = new(LanguageVersion.Preview);

    /// <summary>Verifies the head rewrite writes the given modifiers and keyword for every supported kind.</summary>
    /// <param name="source">The declaration source.</param>
    /// <param name="expected">The declaration after the rewrite.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }", "internal /*k*/class C { }")]
    [Arguments("struct C { }", "internal /*k*/struct C { }")]
    [Arguments("interface C { }", "internal /*k*/interface C { }")]
    [Arguments("record C;", "internal /*k*/record C;")]
    [Arguments("record struct C(int X);", "internal /*k*/record struct C(int X);")]
    public async Task WithHeadWritesModifiersAndKeywordAsync(string source, string expected)
    {
        var declaration = ParseType(source);
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(default, SyntaxKind.InternalKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)));
        var keyword = declaration.Keyword.WithLeadingTrivia(SyntaxFactory.Comment("/*k*/"));

        var rewritten = TypeDeclarationRewrite.WithHead(declaration, modifiers, keyword);

        await Assert.That(rewritten!.ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies the body rewrite writes the given closing tokens for every supported kind.</summary>
    /// <param name="source">The declaration source.</param>
    /// <param name="expected">The declaration after the rewrite.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { };", "class C { }")]
    [Arguments("struct C { };", "struct C { }")]
    [Arguments("interface C { };", "interface C { }")]
    public async Task WithBodyWritesClosingTokensAsync(string source, string expected)
    {
        var declaration = ParseType(source);

        var rewritten = TypeDeclarationRewrite.WithBody(
            declaration,
            declaration.ParameterList,
            declaration.BaseList,
            declaration.Members,
            declaration.CloseBraceToken,
            default);

        await Assert.That(rewritten!.ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies the body rewrite writes a parameter list, base list and members.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WithBodyWritesParameterBaseAndMembersAsync()
    {
        var declaration = ParseType("class C { int x; }");
        var parameters = SyntaxFactory.ParseParameterList("(int x)");
        var baseList = SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("B"))));

        var rewritten = TypeDeclarationRewrite.WithBody(declaration, parameters, baseList, default, declaration.CloseBraceToken, declaration.SemicolonToken);

        await Assert.That(rewritten!.NormalizeWhitespace(eol: "\n").ToFullString()).IsEqualTo("class C(int x) : B\n{\n}");
    }

    /// <summary>Verifies a type declaration of another kind is left for the caller.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OtherKindsReturnNullAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("static class E { extension(int value) { } }", options: PreviewOptions);
        var block = root.DescendantNodes().OfType<TypeDeclarationSyntax>().Single(static type => type is not ClassDeclarationSyntax);

        await Assert.That(TypeDeclarationRewrite.WithHead(block, block.Modifiers, block.Keyword)).IsNull();
        await Assert.That(TypeDeclarationRewrite.WithBody(block, block.ParameterList, block.BaseList, block.Members, block.CloseBraceToken, block.SemicolonToken)).IsNull();
    }

    /// <summary>Verifies a record is left for the caller when its body is rewritten.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WithBodyLeavesRecordsToTheCallerAsync()
    {
        var record = ParseType("record C { };");

        await Assert.That(TypeDeclarationRewrite.WithBody(record, record.ParameterList, record.BaseList, record.Members, record.CloseBraceToken, default)).IsNull();
    }

    /// <summary>Parses a single type declaration.</summary>
    /// <param name="source">The declaration source.</param>
    /// <returns>The parsed declaration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeDeclarationSyntax ParseType(string source) =>
        SyntaxFactory.ParseCompilationUnit(source, options: PreviewOptions).DescendantNodes().OfType<TypeDeclarationSyntax>().First();
}
