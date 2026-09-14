// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests resolving and rewriting the documentation element a diagnostic was reported inside.</summary>
public class DocumentationElementFixTests
{
    /// <summary>A class whose constructor and method both carry a summary.</summary>
    private const string Source = "class C\n{\n    /// <summary>Makes one.</summary>\n    public C() { }\n\n    /// <summary>Runs.</summary>\n    public void M() { }\n}\n";

    /// <summary>The position of the class name in <see cref="RenamedSource"/>.</summary>
    private const int ClassNameStart = 6;

    /// <summary>A class the text-change test renames.</summary>
    private const string RenamedSource = "class A { }";

    /// <summary>Verifies the element surrounding the reported text is found.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FindElementReturnsTheSurroundingElementAsync()
    {
        var root = await CSharpSyntaxTree.ParseText(Source).GetRootAsync();

        var element = DocumentationElementFix.FindElement(root, DiagnosticAt(root, "Makes"));

        await Assert.That(element!.StartTag.Name.LocalName.ValueText).IsEqualTo("summary");
        await Assert.That(element.Content.ToString()).IsEqualTo("Makes one.");
    }

    /// <summary>Verifies a diagnostic outside any documentation resolves no element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FindElementOutsideDocumentationReturnsNullAsync()
    {
        var root = await CSharpSyntaxTree.ParseText(Source).GetRootAsync();

        await Assert.That(DocumentationElementFix.FindElement(root, DiagnosticAt(root, "class"))).IsNull();
    }

    /// <summary>Verifies the summary resolves only when it documents the requested member kind.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryFindMemberSummaryMatchesTheMemberKindAsync()
    {
        var root = await CSharpSyntaxTree.ParseText(Source).GetRootAsync();

        var found = DocumentationElementFix.TryFindMemberSummary<ConstructorDeclarationSyntax>(root, DiagnosticAt(root, "Makes"), out var summary, out var type);
        var mismatched = DocumentationElementFix.TryFindMemberSummary<ConstructorDeclarationSyntax>(root, DiagnosticAt(root, "Runs"), out _, out _);

        await Assert.That(found).IsTrue();
        await Assert.That(summary!.Content.ToString()).IsEqualTo("Makes one.");
        await Assert.That(type!.Identifier.ValueText).IsEqualTo("C");
        await Assert.That(mismatched).IsFalse();
    }

    /// <summary>Verifies the summary replacement covers the whole element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReplaceSummaryRewritesTheWholeElementAsync()
    {
        var root = await CSharpSyntaxTree.ParseText(Source).GetRootAsync();
        var element = DocumentationElementFix.FindElement(root, DiagnosticAt(root, "Runs"))!;

        var change = DocumentationElementFix.ReplaceSummary(element, "Walks.");

        await Assert.That(change.Span).IsEqualTo(element.Span);
        await Assert.That(change.NewText).IsEqualTo("<summary>Walks.</summary>");
    }

    /// <summary>Verifies one change is written into the document's text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ApplyAsyncWritesTheChangeAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(DocumentationElementFix), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(RenamedSource));

        var changed = await DocumentationElementFix.ApplyAsync(document, new(new(ClassNameStart, 1), "B"), CancellationToken.None);

        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo("class B { }");
    }

    /// <summary>Creates a diagnostic spanning the first occurrence of some text.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="text">The text the diagnostic covers.</param>
    /// <returns>The diagnostic.</returns>
    private static Diagnostic DiagnosticAt(SyntaxNode root, string text)
    {
        var start = Source.IndexOf(text, StringComparison.Ordinal);
        return Diagnostic.Create(DocumentationRules.ConstructorStandardText, Location.Create(root.SyntaxTree, new(start, text.Length)));
    }
}
