// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests XML navigation, prose boundaries, and reference-aware comparison keys.</summary>
public sealed class XmlDocumentationHelperTests
{
    /// <summary>Verifies name and cref accessors handle paired, empty, and non-element nodes.</summary>
    /// <param name="xml">The documentation node.</param>
    /// <param name="name">The expected name attribute.</param>
    /// <param name="cref">The expected simple cref name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("<param name=\"value\"/>", "value", null)]
    [Arguments("<param name=\"value\">Value.</param>", "value", null)]
    [Arguments("<see cref=\"C.Method(int)\"/>", null, "Method")]
    [Arguments("<see cref=\"C.Method{T}(T)\">Link.</see>", null, "Method")]
    [Arguments("<see cref=\"global::C\"/>", null, "C")]
    [Arguments("<see cref=\"C\"/>", null, "C")]
    [Arguments("<summary>Text.</summary>", null, null)]
    [Arguments("<summary/>", null, null)]
    [Arguments("<!-- comment -->", null, null)]
    public async Task AttributeNavigationRespectsNodeShapeAsync(string xml, string? name, string? cref)
    {
        var node = ParseNode(xml);
        await Assert.That(XmlDocumentationHelper.NameAttribute(node)).IsEqualTo(name);
        await Assert.That(XmlDocumentationHelper.CrefSimpleName(node)).IsEqualTo(cref);
        await Assert.That(XmlDocumentationHelper.HasCref(node)).IsEqualTo(cref is not null);
    }

    /// <summary>Verifies non-elements have no prose or normalized content.</summary>
    /// <param name="xml">The empty or non-element node.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("<summary/>")]
    [Arguments("<!-- comment -->")]
    public async Task NonElementHasNoProseAsync(string xml)
    {
        var node = ParseNode(xml);
        await Assert.That(XmlDocumentationHelper.HasText(node)).IsFalse();
        await Assert.That(XmlDocumentationHelper.NormalizedText(node)).IsEqualTo(string.Empty);
    }

    /// <summary>Verifies leading prose skips whitespace and line breaks but does not skip inline elements.</summary>
    /// <param name="content">The summary content.</param>
    /// <param name="expected">Whether the leading prose begins with the expected verb.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("   Gets a value.", true)]
    [Arguments("\n   Gets a value.", true)]
    [Arguments("   Sets a value.", false)]
    [Arguments("<see cref=\"C\"/> Gets a value.", false)]
    [Arguments("   ", false)]
    [Arguments("", false)]
    [Arguments("&amp;", false)]
    public async Task LeadingProseHonorsFirstSignificantNodeAsync(string content, bool expected)
    {
        var element = (XmlElementSyntax)ParseNode($"<summary>{content}</summary>");
        await Assert.That(XmlDocumentationHelper.LeadingTextStartsWith(element, "Gets")).IsEqualTo(expected);
    }

    /// <summary>Verifies character scans retain absolute text positions and ignore trailing whitespace.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CharacterScansReturnSourcePositionsAsync()
    {
        const string Source = "/** <summary>  Alpha beta  </summary> */ class C { }";
        var member = SyntaxFactory.ParseCompilationUnit(Source).Members[0];
        var documentation = XmlDocumentationHelper.GetDocumentationComment(member)!;
        var element = (XmlElementSyntax)XmlDocumentationHelper.FindElement(documentation, "summary")!;
        await Assert.That(XmlDocumentationHelper.TryGetFirstTextCharacter(element, out var first, out var firstPosition)).IsTrue();
        await Assert.That(first).IsEqualTo('A');
        await Assert.That(firstPosition).IsEqualTo(Source.IndexOf("Alpha", StringComparison.Ordinal));
        await Assert.That(XmlDocumentationHelper.TryGetLastTextCharacter(element, out var last, out var lastPosition)).IsTrue();
        await Assert.That(last).IsEqualTo('a');
        await Assert.That(lastPosition).IsEqualTo(Source.IndexOf("beta", StringComparison.Ordinal) + "beta".Length - 1);
        await Assert.That(XmlDocumentationHelper.DocumentedMember(element)).IsEqualTo(member);
        await Assert.That(XmlDocumentationHelper.DocumentedMember(member)).IsNull();
    }

    /// <summary>Verifies empty prose returns no character and the absent-position sentinel.</summary>
    /// <param name="content">The summary content without literal prose.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments(" \n ")]
    [Arguments("<see cref=\"C\"/>")]
    public async Task EmptyProseHasNoCharacterAsync(string content)
    {
        var element = (XmlElementSyntax)ParseNode($"<summary>{content}</summary>");
        await Assert.That(XmlDocumentationHelper.TryGetFirstTextCharacter(element, out _, out var firstPosition)).IsFalse();
        await Assert.That(firstPosition).IsEqualTo(-1);
        await Assert.That(XmlDocumentationHelper.TryGetLastTextCharacter(element, out _, out var lastPosition)).IsFalse();
        await Assert.That(lastPosition).IsEqualTo(-1);
    }

    /// <summary>Verifies duplicate keys distinguish inline reference targets while ignoring the outer parameter name.</summary>
    /// <param name="content">The parameter content.</param>
    /// <param name="expected">The normalized comparison key.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("  Alpha   beta  ", "Alpha beta")]
    [Arguments("<paramref name=\"value\"/>", "value")]
    [Arguments("Use <typeparamref name=\"T\"/>", "Use T")]
    [Arguments("<see langword=\"null\"/>", "null")]
    [Arguments("Use <a href=\"target\">link</a>", "Use targetlink")]
    [Arguments("<see cref=\"C.M(int)\"/>", "C.M(int)")]
    [Arguments("<c> nested   text </c>", "nested text")]
    [Arguments("<!-- comment -->", "")]
    [Arguments("\n Alpha \n beta ", "Alpha beta")]
    public async Task DuplicateKeyIncludesNestedReferenceTargetsAsync(string content, string expected)
    {
        var element = (XmlElementSyntax)ParseNode($"<param name=\"ignored\">{content}</param>");
        var builder = new StringBuilder();
        XmlDocumentationHelper.AppendDuplicateComparisonKey(element, builder);
        await Assert.That(builder.ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies terminal punctuation and closing delimiters determine whether a period is required.</summary>
    /// <param name="content">The final summary content.</param>
    /// <param name="expected">Whether a period must be inserted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Text", true)]
    [Arguments("Text.", false)]
    [Arguments("Text!", false)]
    [Arguments("Text?", false)]
    [Arguments("Text:", false)]
    [Arguments("Text;", false)]
    [Arguments("(Text.)", false)]
    [Arguments("[Text.]", false)]
    [Arguments("Text.'", false)]
    [Arguments("Text.\"", false)]
    [Arguments("Text.”", false)]
    [Arguments("Text.’", false)]
    [Arguments("(Text)", true)]
    [Arguments("Text(", true)]
    [Arguments("Text &amp;", true)]
    [Arguments("Text <see cref=\"C\"/>", false)]
    [Arguments("   ", false)]
    public async Task TerminalPeriodHonorsPunctuationAsync(string content, bool expected)
    {
        var element = (XmlElementSyntax)ParseNode($"<summary>{content}</summary>");
        await Assert.That(XmlDocumentationHelper.NeedsTerminalPeriod(element, out var position)).IsEqualTo(expected);
        await Assert.That(position >= 0).IsEqualTo(expected);
    }

    /// <summary>Verifies named lookup distinguishes absent, empty, and paired parameter elements.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NamedLookupFindsMatchingParameterAsync()
    {
        var documentation = ParseDocumentation("<param name=\"other\"/><param name=\"value\">Text.</param><typeparam name=\"T\"/>");
        await Assert.That(XmlDocumentationHelper.FindParameterElement(documentation, "value")).IsNotNull();
        await Assert.That(XmlDocumentationHelper.FindParameterElement(documentation, "missing")).IsNull();
        await Assert.That(XmlDocumentationHelper.FindTypeParameterElement(documentation, "T")).IsNotNull();
        await Assert.That(XmlDocumentationHelper.FindTypeParameterElement(documentation, "Missing")).IsNull();
    }

    /// <summary>Verifies partial-content documentation requires both a partial declaration and meaningful prose.</summary>
    /// <param name="declaration">The documented declaration.</param>
    /// <param name="xml">The documentation content.</param>
    /// <param name="expected">Whether the declaration documents another partial part.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("partial class C { }", "<content>Additional members.</content>", true)]
    [Arguments("partial class C { }", "<content> </content>", false)]
    [Arguments("partial class C { }", "<content/>", false)]
    [Arguments("partial class C { }", "<summary>Primary part.</summary>", false)]
    [Arguments("class C { }", "<content>Additional members.</content>", false)]
    public async Task PartialContentRequiresMeaningfulTextAsync(string declaration, string xml, bool expected)
    {
        var member = SyntaxFactory.ParseCompilationUnit(declaration).Members[0];
        var documentation = ParseDocumentation(xml);
        await Assert.That(XmlDocumentationHelper.DocumentsPartialContent(member, documentation)).IsEqualTo(expected);
        await Assert.That(XmlDocumentationHelper.DocumentsPartialContent(SyntaxFactory.IdentifierName("C"), documentation)).IsFalse();
    }

    /// <summary>Verifies ordinary comments are skipped while multiline XML documentation remains discoverable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocumentationLookupSkipsOrdinaryTriviaAsync()
    {
        var plain = SyntaxFactory.ParseCompilationUnit("/* ordinary */ class C { }").Members[0];
        var documented = SyntaxFactory.ParseCompilationUnit("/* ordinary */ /** <summary>Text.</summary> */ class C { }").Members[0];
        await Assert.That(XmlDocumentationHelper.GetDocumentationComment(plain)).IsNull();
        await Assert.That(XmlDocumentationHelper.GetDocumentationComment(documented)).IsNotNull();
    }

    /// <summary>Verifies normalized prose collapses whitespace across nested elements.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NormalizedProseCollapsesWhitespaceAsync()
    {
        var element = ParseNode("<summary>  Alpha <c> beta </c> gamma  </summary>");
        await Assert.That(XmlDocumentationHelper.HasText(element)).IsTrue();
        await Assert.That(XmlDocumentationHelper.NormalizedText(element)).IsEqualTo("Alpha beta gamma");
    }

    /// <summary>Verifies inherited documentation needs an explicit cref before it names an inheritance source.</summary>
    /// <param name="xml">The documentation content.</param>
    /// <param name="expected">Whether an inheritdoc element names its source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("<summary>Text.</summary>", false)]
    [Arguments("<inheritdoc/>", false)]
    [Arguments("<inheritdoc cref=\"C\"/>", true)]
    [Arguments("<inheritdoc cref=\"C\"></inheritdoc>", true)]
    public async Task InheritedDocumentationRequiresExplicitReferenceAsync(string xml, bool expected)
    {
        var documentation = ParseDocumentation(xml);
        await Assert.That(XmlDocumentationHelper.HasInheritDocCref(documentation)).IsEqualTo(expected);
    }

    /// <summary>Parses a documentation comment attached to a class.</summary>
    /// <param name="xml">The XML content.</param>
    /// <returns>The parsed documentation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DocumentationCommentTriviaSyntax ParseDocumentation(string xml) =>
        XmlDocumentationHelper.GetDocumentationComment(SyntaxFactory.ParseCompilationUnit($"/** {xml} */ class C {{ }}").Members[0])!;

    /// <summary>Parses the first non-text node in a documentation comment.</summary>
    /// <param name="xml">The XML node.</param>
    /// <returns>The parsed node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static XmlNodeSyntax ParseNode(string xml) =>
        ParseDocumentation(xml).Content.First(static node => node is not XmlTextSyntax);
}
