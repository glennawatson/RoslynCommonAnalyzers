// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Helper-level tests for shared layout cursor utilities.</summary>
public sealed class LayoutHelpersUnitTest
{
    /// <summary>Verifies the shared cursor can resolve both start and end lines for later spans.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetLineSpanOfOrLaterResolvesMultiLineSpanAsync()
    {
        var text = SourceText.From("first\nsecond\nthird\n");
        var lineNumber = 0;
        var line = text.Lines[0];

        LayoutHelpers.GetLineSpanOfOrLater(text, 6, 14, ref lineNumber, ref line, out var startLine, out var endLine);

        await Assert.That(startLine).IsEqualTo(1);
        await Assert.That(endLine).IsEqualTo(2);
    }

    /// <summary>Verifies the shared line cursor advances monotonically across later positions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LineOfOrLaterAdvancesAcrossLaterPositionsAsync()
    {
        var text = SourceText.From("first\nsecond\nthird\n");
        var lineNumber = 0;
        var line = text.Lines[0];

        var firstLine = LayoutHelpers.LineOfOrLater(text, 0, ref lineNumber, ref line);
        var secondLine = LayoutHelpers.LineOfOrLater(text, 7, ref lineNumber, ref line);
        var thirdLine = LayoutHelpers.LineOfOrLater(text, 14, ref lineNumber, ref line);

        await Assert.That(firstLine).IsEqualTo(0);
        await Assert.That(secondLine).IsEqualTo(1);
        await Assert.That(thirdLine).IsEqualTo(2);
    }

    /// <summary>Verifies line-relationship helpers classify an Allman opening brace correctly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BraceLineHelpersClassifyAllmanOpeningBraceAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    void M()\n    {\n    }\n}");
        var block = root.DescendantNodes().OfType<BlockSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();
        var open = block.OpenBraceToken;
        var openLine = LayoutHelpers.StartLine(text, open);

        await Assert.That(LayoutHelpers.TokenStartsLine(text, open, openLine)).IsTrue();
        await Assert.That(LayoutHelpers.TokenSharesLineWithPrevious(text, open, openLine)).IsFalse();
        await Assert.That(LayoutHelpers.TokenSharesLineWithNext(text, open, openLine)).IsFalse();
    }

    /// <summary>Verifies line-relationship helpers classify an inline opening brace correctly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BraceLineHelpersClassifyInlineOpeningBraceAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    void M() { return; }\n}");
        var block = root.DescendantNodes().OfType<BlockSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();
        var open = block.OpenBraceToken;
        var openLine = LayoutHelpers.StartLine(text, open);

        await Assert.That(LayoutHelpers.TokenStartsLine(text, open, openLine)).IsFalse();
        await Assert.That(LayoutHelpers.TokenSharesLineWithPrevious(text, open, openLine)).IsTrue();
        await Assert.That(LayoutHelpers.TokenSharesLineWithNext(text, open, openLine)).IsTrue();
    }

    /// <summary>Verifies content start falls back directly to the first token when no header trivia exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContentStartLineUsesFirstTokenWithoutLeadingCommentsAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    void M() { }\n}");
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();

        await Assert.That(LayoutHelpers.ContentStartLine(text, method)).IsEqualTo(2);
    }

    /// <summary>Verifies content start still honors leading comment trivia when present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContentStartLineUsesLeadingCommentWhenPresentAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    // docs\n    void M() { }\n}");
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();

        await Assert.That(LayoutHelpers.ContentStartLine(text, method)).IsEqualTo(2);
    }

    /// <summary>Verifies the header-trivia helper exits immediately when no leading trivia exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetHeaderStartLineSkipsMembersWithoutLeadingTriviaAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    void M() { }\n}");
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();

        await Assert.That(LayoutHelpers.TryGetHeaderStartLine(text, method, out _)).IsFalse();
    }

    /// <summary>Verifies the header-trivia helper finds the start line of leading comment trivia.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetHeaderStartLineFindsLeadingCommentAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    // docs\n    void M() { }\n}");
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();

        await Assert.That(LayoutHelpers.TryGetHeaderStartLine(text, method, out var startLine)).IsTrue();
        await Assert.That(startLine).IsEqualTo(2);
    }

    /// <summary>Verifies cursor-aware content-start lookup reuses the running line cursor without changing behavior.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContentStartLineOrLaterUsesCursorWithoutHeaderTriviaAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    void M() { }\n    void N() { }\n}");
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var text = await root.SyntaxTree.GetTextAsync();
        var lineNumber = 0;
        var line = text.Lines[0];

        var firstLine = LayoutHelpers.ContentStartLineOrLater(text, methods[0], ref lineNumber, ref line);
        var secondLine = LayoutHelpers.ContentStartLineOrLater(text, methods[1], ref lineNumber, ref line);

        await Assert.That(firstLine).IsEqualTo(2);
        await Assert.That(secondLine).IsEqualTo(3);
    }

    /// <summary>Verifies cursor-aware content-start lookup still honors leading comment trivia.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContentStartLineOrLaterUsesHeaderTriviaWhenPresentAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    // docs\n    void M() { }\n}");
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();
        var lineNumber = 0;
        var line = text.Lines[0];

        await Assert.That(LayoutHelpers.ContentStartLineOrLater(text, method, ref lineNumber, ref line)).IsEqualTo(2);
    }

    /// <summary>Verifies a combined token-line facts helper captures both previous and next sharing facts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TokenLineFactsClassifyInlineBraceAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    void M() { return; }\n}");
        var block = root.DescendantNodes().OfType<BlockSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();
        var open = block.OpenBraceToken;
        var openLine = LayoutHelpers.StartLine(text, open);

        await Assert.That(LayoutHelpers.GetTokenLineFacts(text, open, openLine).StartsLine).IsFalse();
        await Assert.That(LayoutHelpers.GetTokenLineFacts(text, open, openLine).SharesLineWithPrevious).IsTrue();
        await Assert.That(LayoutHelpers.GetTokenLineFacts(text, open, openLine).SharesLineWithNext).IsTrue();
    }

    /// <summary>Verifies a combined token-line facts helper classifies an Allman brace cleanly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TokenLineFactsClassifyAllmanBraceAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C\n{\n    void M()\n    {\n        return;\n    }\n}");
        var block = root.DescendantNodes().OfType<BlockSyntax>().Single();
        var text = await root.SyntaxTree.GetTextAsync();
        var open = block.OpenBraceToken;
        var openLine = LayoutHelpers.StartLine(text, open);

        var facts = LayoutHelpers.GetTokenLineFacts(text, open, openLine);
        await Assert.That(facts.StartsLine).IsTrue();
        await Assert.That(facts.SharesLineWithPrevious).IsFalse();
        await Assert.That(facts.SharesLineWithNext).IsFalse();
    }
}
