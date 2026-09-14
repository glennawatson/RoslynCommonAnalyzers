// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests whitespace edits and comment boundaries shared by layout fixes.</summary>
public class LayoutFixHelpersTests
{
    /// <summary>Checks empty text, unterminated lines, and both supported newline sequences.</summary>
    /// <param name="source">The source text.</param>
    /// <param name="expected">The detected newline.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "\n")]
    [Arguments("value", "\n")]
    [Arguments("value\nnext", "\n")]
    [Arguments("value\r\nnext", "\r\n")]
    public async Task DetectsDocumentNewlineAsync(string source, string expected)
    {
        var text = SourceText.From(source);
        await Assert.That(LayoutFixHelpers.DetectNewLine(text)).IsEqualTo(expected);
        await Assert.That(text.Lines.Count > 0).IsTrue();
    }

    /// <summary>Checks indentation stops at content or the end of a whitespace-only line.</summary>
    /// <param name="source">The single line.</param>
    /// <param name="expected">The indentation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "")]
    [Arguments(" \t", " \t")]
    [Arguments(" \tvalue", " \t")]
    [Arguments("value", "")]
    public async Task ReadsLineIndentationAsync(string source, string expected)
    {
        var text = SourceText.From(source);
        await Assert.That(LayoutFixHelpers.IndentOfLine(text, 0)).IsEqualTo(expected);
    }

    /// <summary>Checks comments before statements or the closing brace prevent block expansion.</summary>
    /// <param name="source">The method source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { /*before*/ return; } }")]
    [Arguments("class C { void M() { return; /*after*/ } }")]
    public async Task BlockExpansionRejectsCommentsAsync(string source)
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);
        var block = root.DescendantNodes().OfType<BlockSyntax>().Single();
        var changes = new List<TextChange>();
        await Assert.That(LayoutFixHelpers.TryAppendBlockExpansion(SourceText.From(source), block, "\n", changes)).IsFalse();
    }

    /// <summary>Checks standalone and inline blocks expand while preserving their contents.</summary>
    /// <param name="source">The statement containing the block.</param>
    /// <param name="expected">The rewritten statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{}", "{\n}")]
    [Arguments("{ return; }", "{\n    return;\n}")]
    [Arguments("if (true) { return; }", "if (true)\n{\n    return;\n}")]
    [Arguments("if (true)\n{ return; }", "if (true)\n{\n    return;\n}")]
    [Arguments("if (true) /*keep*/ { return; }", "if (true) /*keep*/ {\n    return;\n}")]
    public async Task ExpandsBlockStatementsAsync(string source, string expected)
    {
        var statement = SyntaxFactory.ParseStatement(source);
        var block = statement.DescendantNodesAndSelf().OfType<BlockSyntax>().Single();
        var text = SourceText.From(source);
        var changes = new List<TextChange>();
        await Assert.That(LayoutFixHelpers.TryAppendBlockExpansion(text, block, "\n", changes)).IsTrue();
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks brace wrapping preserves comments by declining to edit their gap.</summary>
    /// <param name="gap">The gap before the embedded statement.</param>
    /// <param name="expected">The resulting statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(" ", "if (true)\n{\n    return;\n}")]
    [Arguments(" /*keep*/ ", "if (true) /*keep*/ return;")]
    public async Task WrapsOnlyCleanEmbeddedStatementGapsAsync(string gap, string expected)
    {
        var source = $"if (true){gap}return;";
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement(source);
        var text = SourceText.From(source);
        var changes = new List<TextChange>();
        LayoutFixHelpers.AppendBraceWrap(text, statement.Statement, "\n", changes);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks switch wrapping declines comments and otherwise encloses the complete section.</summary>
    /// <param name="gap">The gap following the case label.</param>
    /// <param name="expected">The resulting switch.</param>
    /// <param name="accepted">Whether wrapping succeeds.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(" ", "switch (x) { case 0:\n{\n    M(); break;\n} }", true)]
    [Arguments(" /*keep*/ ", "switch (x) { case 0: /*keep*/ M(); break; }", false)]
    public async Task WrapsOnlyCleanSwitchSectionGapsAsync(string gap, string expected, bool accepted)
    {
        var source = $"switch (x) {{ case 0:{gap}M(); break; }}";
        var statement = (SwitchStatementSyntax)SyntaxFactory.ParseStatement(source);
        var section = statement.Sections[0];
        var text = SourceText.From(source);
        var changes = new List<TextChange>();
        await Assert.That(LayoutFixHelpers.TryAppendSwitchSectionBraceWrap(
            text,
            section.Statements[0],
            section.Statements[^1],
            "\n",
            changes)).IsEqualTo(accepted);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks comments on either side of an operator prevent moving its line break.</summary>
    /// <param name="source">The binary expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a /*keep*/ +\n b")]
    [Arguments("a + /*keep*/\n b")]
    public async Task OperatorBreakMoveRejectsCommentsAsync(string source)
    {
        var expression = (BinaryExpressionSyntax)SyntaxFactory.ParseExpression(source);
        var changes = new List<TextChange>();
        await Assert.That(LayoutFixHelpers.TryAppendTokenBreakMove(SourceText.From(source), expression.OperatorToken, false, true, "\n", changes)).IsFalse();
        await Assert.That(changes).IsEmpty();
    }

    /// <summary>Checks tokens without a previous or next token cannot move a line break.</summary>
    /// <param name="first">Whether to select the first rather than the last token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task OperatorBreakMoveRequiresNeighborsAsync(bool first)
    {
        const string Source = "a + b";
        var expression = SyntaxFactory.ParseExpression(Source);
        var token = first ? expression.GetFirstToken() : expression.GetLastToken();
        var changes = new List<TextChange>();
        await Assert.That(LayoutFixHelpers.TryAppendTokenBreakMove(SourceText.From(Source), token, false, true, "\n", changes)).IsFalse();
        await Assert.That(changes).IsEmpty();
    }

    /// <summary>Checks operator moves retain the existing continuation indentation in both directions.</summary>
    /// <param name="source">The binary expression.</param>
    /// <param name="before">Whether the break precedes the operator.</param>
    /// <param name="expected">The rewritten expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a +\n  b", false, "a\n  + b")]
    [Arguments("a\n  + b", true, "a +\n  b")]
    public async Task MovesOperatorBreakAsync(string source, bool before, string expected)
    {
        var expression = (BinaryExpressionSyntax)SyntaxFactory.ParseExpression(source);
        var text = SourceText.From(source);
        var changes = new List<TextChange>();
        await Assert.That(LayoutFixHelpers.TryAppendTokenBreakMove(text, expression.OperatorToken, before, !before, "\n", changes)).IsTrue();
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks member-chain moves preserve comments and use the document newline.</summary>
    /// <param name="source">The member access.</param>
    /// <param name="before">Whether the break precedes the dot.</param>
    /// <param name="accepted">Whether the surrounding gaps can be edited.</param>
    /// <param name="expected">The resulting expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a.\r\n  B", false, true, "a\r\n  .B")]
    [Arguments("a\n  .B", true, true, "a.\n  B")]
    [Arguments("a /*keep*/ .\n  B", false, false, "a /*keep*/ .\n  B")]
    [Arguments("a. /*keep*/\n  B", false, false, "a. /*keep*/\n  B")]
    public async Task MovesOnlyCleanChainBreaksAsync(string source, bool before, bool accepted, string expected)
    {
        var expression = (MemberAccessExpressionSyntax)SyntaxFactory.ParseExpression(source);
        var text = SourceText.From(source);
        var changes = new List<TextChange>();
        await Assert.That(LayoutFixHelpers.TryAppendChainLinkBreakMove(
            text,
            expression.OperatorToken,
            expression.OperatorToken,
            expression.Name.GetFirstToken(),
            before,
            !before,
            changes)).IsEqualTo(accepted);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }
}
