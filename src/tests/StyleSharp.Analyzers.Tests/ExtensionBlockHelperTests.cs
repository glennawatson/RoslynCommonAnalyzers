// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests extension receiver classification and syntax recognition.</summary>
public class ExtensionBlockHelperTests
{
    /// <summary>Checks receiver text, cheap classification, and broad-receiver recognition independently.</summary>
    /// <param name="text">The receiver syntax, or null when absent.</param>
    /// <param name="shape">The cheaply classified shape.</param>
    /// <param name="broad">Whether the cheap classification identifies a broad receiver.</param>
    /// <param name="broadText">The broad-receiver display text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(null, null, false, "")]
    [Arguments("int", "int", false, "")]
    [Arguments("object", "object", true, "object")]
    [Arguments("dynamic", "dynamic", true, "dynamic")]
    [Arguments("@Value", "Value", false, "")]
    [Arguments("System.Object", null, false, "System.Object")]
    [Arguments("Other.Object", null, false, "")]
    [Arguments("System.String", null, false, "")]
    [Arguments("global::System.Object", null, false, "")]
    [Arguments("int[]", null, false, "")]
    public async Task ReceiverShapesAreClassifiedAsync(string? text, string? shape, bool broad, string broadText)
    {
        var type = text is null ? null : SyntaxFactory.ParseTypeName(text);
        await Assert.That(ExtensionBlockHelper.ReceiverTypeText(type)).IsEqualTo(text);
        await Assert.That(ExtensionBlockHelper.TryClassifyReceiverShape(type, out var actualShape)).IsEqualTo(shape is not null);
        await Assert.That(actualShape).IsEqualTo(shape);
        await Assert.That(ExtensionBlockHelper.TryClassifyReceiver(type, out actualShape, out var actualBroad)).IsEqualTo(shape is not null);
        await Assert.That(actualShape).IsEqualTo(shape);
        await Assert.That(actualBroad).IsEqualTo(broad);
        await Assert.That(ExtensionBlockHelper.IsBroadReceiver(type, out var actualText)).IsEqualTo(broadText.Length > 0);
        await Assert.That(actualText).IsEqualTo(broadText);
    }

    /// <summary>Checks absent and empty parameter lists have no receiver.</summary>
    /// <param name="source">The type declaration.</param>
    /// <param name="expected">The receiver text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C {}", null)]
    [Arguments("struct C {}", null)]
    [Arguments("record C;", null)]
    [Arguments("record struct C;", null)]
    [Arguments("interface C {}", null)]
    [Arguments("class C() {}", null)]
    [Arguments("class C(string value) {}", "string")]
    [Arguments("class C(Value value) {}", "Value")]
    [Arguments("class C(System.String value) {}", "System.String")]
    public async Task ReceiverTypeHandlesParameterListsAsync(string source, string? expected)
    {
        var declaration = (TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0];
        await Assert.That(ExtensionBlockHelper.ReceiverTypeText(declaration)).IsEqualTo(expected);
        await Assert.That(ExtensionBlockHelper.IsExtensionBlock(declaration)).IsFalse();
    }

    /// <summary>Checks a block's typed receiver is read, whether or not the receiver is named.</summary>
    /// <param name="block">The extension block.</param>
    /// <param name="expectedName">The receiver's name, empty when unnamed.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("extension(string text) { }", "text")]
    [Arguments("extension(string) { }", "")]
    public async Task TryGetReceiverReadsTheTypedReceiverAsync(string block, string expectedName)
    {
        var extension = ParseExtensionBlock(block);

        await Assert.That(ExtensionBlockHelper.TryGetReceiver(extension, out var receiver, out var receiverType)).IsTrue();
        await Assert.That(receiver.Identifier.ValueText).IsEqualTo(expectedName);
        await Assert.That(receiverType.ToString()).IsEqualTo("string");
    }

    /// <summary>Checks a declaration without a parameter list has no receiver to read.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclarationWithoutParameterListHasNoReceiverAsync()
    {
        var declaration = (TypeDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("class Nested { }")!;

        await Assert.That(ExtensionBlockHelper.TryGetReceiver(declaration, out _, out _)).IsFalse();
    }

    /// <summary>Checks classic extension methods require a first this parameter.</summary>
    /// <param name="source">The member source.</param>
    /// <param name="expected">Whether the member is a classic extension method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("static void M(this string value) {}", true)]
    [Arguments("static void M(string value) {}", false)]
    [Arguments("static void M() {}", false)]
    [Arguments("int Value;", false)]
    public async Task ClassicExtensionRequiresThisParameterAsync(string source, bool expected)
    {
        var member = SyntaxFactory.ParseMemberDeclaration(source)!;
        await Assert.That(ExtensionBlockHelper.IsClassicExtensionMethod(member)).IsEqualTo(expected);
        await Assert.That(ExtensionBlockHelper.IsExtensionBlock(member)).IsFalse();
    }

    /// <summary>Checks token reads honor identifier value text and reject unrelated tokens.</summary>
    /// <param name="source">The expression to scan.</param>
    /// <param name="expected">Whether the receiver is read.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("other + 1", false)]
    [Arguments("@receiver.Length", true)]
    [Arguments("\"receiver\"", false)]
    public async Task IdentifierReadsUseIdentifierTokensAsync(string source, bool expected)
    {
        var expression = SyntaxFactory.ParseExpression(source);
        await Assert.That(ExtensionBlockHelper.ReadsIdentifier(expression, "receiver")).IsEqualTo(expected);
    }

    /// <summary>Checks only the synthesized extension container is recognized semantically.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtensionContainerRequiresExtensionSymbolAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("static class C { extension(string value) { public int Size() => value.Length; } }", new(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create("Extensions", [tree], RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var outer = (ClassDeclarationSyntax)((CompilationUnitSyntax)root).Members[0];
        var extension = (TypeDeclarationSyntax)outer.Members[0];
        var model = compilation.GetSemanticModel(tree);
        await Assert.That(ExtensionBlockHelper.IsExtensionBlock(extension)).IsTrue();
        await Assert.That(ExtensionBlockHelper.IsExtensionContainer(model.GetDeclaredSymbol(extension))).IsTrue();
        await Assert.That(ExtensionBlockHelper.IsExtensionContainer(model.GetDeclaredSymbol(outer))).IsFalse();
        await Assert.That(ExtensionBlockHelper.IsExtensionContainer(null)).IsFalse();
    }

    /// <summary>Parses one extension block inside a static class.</summary>
    /// <param name="block">The extension block source.</param>
    /// <returns>The parsed extension block.</returns>
    private static TypeDeclarationSyntax ParseExtensionBlock(string block)
    {
        var unit = SyntaxFactory.ParseCompilationUnit($"static class C {{ {block} }}", options: new(LanguageVersion.Preview));
        return (TypeDeclarationSyntax)((ClassDeclarationSyntax)unit.Members[0]).Members[0];
    }
}
