// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Helper-level tests for shared layout cursor utilities.</summary>
public sealed class LayoutHelpersUnitTest
{
    /// <summary>Zero-based index of "third", the last line of the three-line cursor fixture.</summary>
    private const int ThirdLineIndex = 2;

    /// <summary>Character position inside "third", the last line of the three-line cursor fixture.</summary>
    private const int ThirdLineInteriorPosition = 14;

    /// <summary>Zero-based line index of the "// docs" leading comment in the commented-method snippets.</summary>
    private const int LeadingCommentLine = 2;

    /// <summary>A method carrying a leading comment, the fixture for the header-trivia lookups.</summary>
    private const string CommentedMethodSource = """
        class C
        {
            // docs
            void M() { }
        }
        """;

    /// <summary>Verifies the shared cursor can resolve both start and end lines for later spans.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetLineSpanOfOrLaterResolvesMultiLineSpanAsync()
    {
        // Normalized to \n so the hard-coded character offsets below do not shift on a CRLF checkout.
        var text = SourceText.From(
            $$"""
            first
            second
            third{{"\n"}}
            """.ReplaceLineEndings("\n"));
        const int SecondLineStartPosition = 6;
        var lineNumber = 0;
        var line = text.Lines[0];

        LayoutHelpers.GetLineSpanOfOrLater(
            text,
            SecondLineStartPosition,
            ThirdLineInteriorPosition,
            ref lineNumber,
            ref line,
            out var startLine,
            out var endLine);

        await Assert.That(startLine).IsEqualTo(1);
        await Assert.That(endLine).IsEqualTo(ThirdLineIndex);
    }

    /// <summary>Verifies the shared line cursor advances monotonically across later positions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LineOfOrLaterAdvancesAcrossLaterPositionsAsync()
    {
        // Normalized to \n so the hard-coded character offsets below do not shift on a CRLF checkout.
        var text = SourceText.From(
            $$"""
            first
            second
            third{{"\n"}}
            """.ReplaceLineEndings("\n"));
        const int SecondLineInteriorPosition = 7;
        var lineNumber = 0;
        var line = text.Lines[0];

        var firstLine = LayoutHelpers.LineOfOrLater(text, 0, ref lineNumber, ref line);
        var secondLine = LayoutHelpers.LineOfOrLater(text, SecondLineInteriorPosition, ref lineNumber, ref line);
        var thirdLine = LayoutHelpers.LineOfOrLater(text, ThirdLineInteriorPosition, ref lineNumber, ref line);

        await Assert.That(firstLine).IsEqualTo(0);
        await Assert.That(secondLine).IsEqualTo(1);
        await Assert.That(thirdLine).IsEqualTo(ThirdLineIndex);
    }

    /// <summary>Verifies line-relationship helpers classify an Allman opening brace correctly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BraceLineHelpersClassifyAllmanOpeningBraceAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit(
            """
            class C
            {
                void M()
                {
                }
            }
            """);
        var block = ParseSingleMethod(root).Body!;
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
        var root = SyntaxFactory.ParseCompilationUnit(
            """
            class C
            {
                void M() { return; }
            }
            """);
        var block = ParseSingleMethod(root).Body!;
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
        var root = SyntaxFactory.ParseCompilationUnit(
            """
            class C
            {
                void M() { }
            }
            """);
        const int MethodDeclarationLine = 2;
        var method = ParseSingleMethod(root);
        var text = await root.SyntaxTree.GetTextAsync();

        await Assert.That(LayoutHelpers.ContentStartLine(text, method)).IsEqualTo(MethodDeclarationLine);
    }

    /// <summary>Verifies content start still honors leading comment trivia when present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContentStartLineUsesLeadingCommentWhenPresentAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit(CommentedMethodSource);
        var method = ParseSingleMethod(root);
        var text = await root.SyntaxTree.GetTextAsync();

        await Assert.That(LayoutHelpers.ContentStartLine(text, method)).IsEqualTo(LeadingCommentLine);
    }

    /// <summary>Verifies the header-trivia helper exits immediately when no leading trivia exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetHeaderStartLineSkipsMembersWithoutLeadingTriviaAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit(
            """
            class C
            {
                void M() { }
            }
            """);
        var method = ParseSingleMethod(root);
        var text = await root.SyntaxTree.GetTextAsync();

        await Assert.That(LayoutHelpers.TryGetHeaderStartLine(text, method, out _)).IsFalse();
    }

    /// <summary>Verifies the header-trivia helper finds the start line of leading comment trivia.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetHeaderStartLineFindsLeadingCommentAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit(CommentedMethodSource);
        var method = ParseSingleMethod(root);
        var text = await root.SyntaxTree.GetTextAsync();

        await Assert.That(LayoutHelpers.TryGetHeaderStartLine(text, method, out var startLine)).IsTrue();
        await Assert.That(startLine).IsEqualTo(LeadingCommentLine);
    }

    /// <summary>Verifies cursor-aware content-start lookup reuses the running line cursor without changing behavior.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContentStartLineOrLaterUsesCursorWithoutHeaderTriviaAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit(
            """
            class C
            {
                void M() { }
                void N() { }
            }
            """);
        const int FirstMethodLine = 2;
        const int SecondMethodLine = 3;
        var methods = ParseMethods(root);
        var text = await root.SyntaxTree.GetTextAsync();
        var lineNumber = 0;
        var line = text.Lines[0];

        var firstLine = LayoutHelpers.ContentStartLineOrLater(text, methods[0], ref lineNumber, ref line);
        var secondLine = LayoutHelpers.ContentStartLineOrLater(text, methods[1], ref lineNumber, ref line);

        await Assert.That(firstLine).IsEqualTo(FirstMethodLine);
        await Assert.That(secondLine).IsEqualTo(SecondMethodLine);
    }

    /// <summary>Verifies cursor-aware content-start lookup still honors leading comment trivia.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContentStartLineOrLaterUsesHeaderTriviaWhenPresentAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit(CommentedMethodSource);
        var method = ParseSingleMethod(root);
        var text = await root.SyntaxTree.GetTextAsync();
        var lineNumber = 0;
        var line = text.Lines[0];
        var contentStartLine = LayoutHelpers.ContentStartLineOrLater(text, method, ref lineNumber, ref line);

        await Assert.That(contentStartLine).IsEqualTo(LeadingCommentLine);
    }

    /// <summary>Verifies a combined token-line facts helper captures both previous and next sharing facts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TokenLineFactsClassifyInlineBraceAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit(
            """
            class C
            {
                void M() { return; }
            }
            """);
        var block = ParseSingleMethod(root).Body!;
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
        var root = SyntaxFactory.ParseCompilationUnit(
            """
            class C
            {
                void M()
                {
                    return;
                }
            }
            """);
        var block = ParseSingleMethod(root).Body!;
        var text = await root.SyntaxTree.GetTextAsync();
        var open = block.OpenBraceToken;
        var openLine = LayoutHelpers.StartLine(text, open);

        var facts = LayoutHelpers.GetTokenLineFacts(text, open, openLine);
        await Assert.That(facts.StartsLine).IsTrue();
        await Assert.That(facts.SharesLineWithPrevious).IsFalse();
        await Assert.That(facts.SharesLineWithNext).IsFalse();
    }

    /// <summary>Verifies invalid line indices are rejected and whitespace-only lines are blank.</summary>
    /// <param name="source">The line content.</param>
    /// <param name="index">The requested line.</param>
    /// <param name="expected">Whether the line is blank.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", -1, false)]
    [Arguments("", 1, false)]
    [Arguments("", 0, true)]
    [Arguments(" \t", 0, true)]
    [Arguments(" x", 0, false)]
    public async Task BlankLineClassificationAsync(string source, int index, bool expected)
    {
        var text = SourceText.From(source);
        await Assert.That(LayoutHelpers.IsBlankLine(text, index)).IsEqualTo(expected);
    }

    /// <summary>Verifies gap classification keeps line breaks separate from non-whitespace content.</summary>
    /// <param name="source">The gap content.</param>
    /// <param name="lineBreak">Whether the gap contains a line break.</param>
    /// <param name="clean">Whether the gap contains only whitespace.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", false, true)]
    [Arguments(" \t", false, true)]
    [Arguments("\r\n", true, true)]
    [Arguments(" /* comment */\n", true, false)]
    [Arguments("x", false, false)]
    public async Task GapClassificationAsync(string source, bool lineBreak, bool clean)
    {
        LayoutHelpers.ClassifyGap(SourceText.From(source), 0, source.Length, out var actualBreak, out var actualClean);
        await Assert.That(actualBreak).IsEqualTo(lineBreak);
        await Assert.That(actualClean).IsEqualTo(clean);
    }

    /// <summary>Verifies each placement uses its cached diagnostic property map.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlacementPropertiesUseCachedMapsAsync()
    {
        await Assert.That(ReferenceEquals(LayoutHelpers.PlacementProperties(true), LayoutHelpers.BreakBeforeProperties)).IsTrue();
        await Assert.That(ReferenceEquals(LayoutHelpers.PlacementProperties(false), LayoutHelpers.BreakAfterProperties)).IsTrue();
        await Assert.That(LayoutHelpers.PlacementProperties(true)[LayoutHelpers.BreakBeforeProperty]).IsEqualTo("true");
        await Assert.That(LayoutHelpers.PlacementProperties(false)[LayoutHelpers.BreakBeforeProperty]).IsEqualTo("false");
    }

    /// <summary>Verifies comment kinds affect content start but only documentation supplies a doc header.</summary>
    /// <param name="header">The trivia before the class.</param>
    /// <param name="isHeader">Whether it contains a comment.</param>
    /// <param name="isDocumentation">Whether it contains documentation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", false, false)]
    [Arguments(" \n", false, false)]
    [Arguments("// header\n", true, false)]
    [Arguments("/* header */\n", true, false)]
    [Arguments("/// <summary>Header.</summary>\n", true, true)]
    [Arguments("/** <summary>Header.</summary> */\n", true, true)]
    public async Task HeaderKindsControlContentStartAsync(string header, bool isHeader, bool isDocumentation)
    {
        var root = SyntaxFactory.ParseCompilationUnit($$"""{{header}}class C { }""");
        var member = root.Members[0];
        var text = await root.SyntaxTree.GetTextAsync();
        var expectedLine = isHeader || header.Length == 0 ? 0 : 1;
        var lineNumber = 0;
        var line = text.Lines[0];

        await Assert.That(LayoutHelpers.TryGetHeaderStartLine(text, member, out var start)).IsEqualTo(isHeader);
        await Assert.That(start).IsEqualTo(0);
        await Assert.That(LayoutHelpers.ContentStartLine(text, member)).IsEqualTo(expectedLine);
        await Assert.That(LayoutHelpers.ContentStartLineOrLater(text, member, ref lineNumber, ref line)).IsEqualTo(expectedLine);
        await Assert.That(LayoutHelpers.TryGetDocHeader(member, out var doc)).IsEqualTo(isDocumentation);
        await Assert.That(doc.RawKind != 0).IsEqualTo(isDocumentation);
    }

    /// <summary>Verifies an end-of-file token has no neighbors and its zero-width span resolves safely.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyTreeTokenHasNoNeighborsAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit(string.Empty);
        var token = root.EndOfFileToken;
        var text = await root.SyntaxTree.GetTextAsync();
        await Assert.That(LayoutHelpers.EndLine(text, token)).IsEqualTo(0);
        await Assert.That(LayoutHelpers.TokenStartsLine(text, token, 0)).IsTrue();
        await Assert.That(LayoutHelpers.TokenSharesLineWithPrevious(text, token, 0)).IsFalse();
        await Assert.That(LayoutHelpers.TokenSharesLineWithNext(text, token, 0)).IsFalse();
        await Assert.That(LayoutHelpers.GetTokenLineFacts(text, token, 0)).IsEqualTo(new(true, false, false));
        await Assert.That(LayoutHelpers.HasLineBreakBefore(token)).IsFalse();
        await Assert.That(LayoutHelpers.HasLineBreakAfter(token)).IsFalse();
    }

    /// <summary>Verifies line breaks can belong to either side of neighboring tokens.</summary>
    /// <param name="trailingBreak">Whether the earlier token carries a trailing break.</param>
    /// <param name="leadingBreak">Whether the later token carries a leading break.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task NeighborTriviaLineBreaksAsync(bool trailingBreak, bool leadingBreak)
    {
        var name = SyntaxFactory.IdentifierName("C").WithLeadingTrivia(leadingBreak ? SyntaxFactory.LineFeed : SyntaxFactory.Space);
        var member = SyntaxFactory.ClassDeclaration(name.Identifier)
            .WithKeyword(SyntaxFactory.Token(SyntaxKind.ClassKeyword).WithTrailingTrivia(trailingBreak ? SyntaxFactory.LineFeed : SyntaxFactory.Space));
        await Assert.That(LayoutHelpers.HasLineBreakAfter(member.Keyword)).IsEqualTo(trailingBreak || leadingBreak);
        await Assert.That(LayoutHelpers.HasLineBreakBefore(member.Identifier)).IsEqualTo(trailingBreak || leadingBreak);
    }

    /// <summary>Verifies the cursor stays on the last line at EOF and keeps a single-line span together.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CursorHandlesEndOfTextAndSingleLineSpanAsync()
    {
        var text = SourceText.From("one");
        var lineNumber = 0;
        var line = text.Lines[0];
        await Assert.That(LayoutHelpers.LineOfOrLater(text, text.Length, ref lineNumber, ref line)).IsEqualTo(0);
        LayoutHelpers.GetLineSpanOfOrLater(text, 0, text.Length, ref lineNumber, ref line, out var start, out var end);
        await Assert.That(start).IsEqualTo(0);
        await Assert.That(end).IsEqualTo(0);
    }

    /// <summary>Verifies calls, indexers and additional members preserve the first conditional binding.</summary>
    /// <param name="source">The chain expression.</param>
    /// <param name="expectedLead">The first link punctuation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a.B", ".")]
    [Arguments("a?.B", "?")]
    [Arguments("a?.B()", "?")]
    [Arguments("a?.B[0]", "?")]
    [Arguments("a?.B.C", "?")]
    public async Task ChainLinkFindsFirstMemberAsync(string source, string expectedLead)
    {
        var node = SyntaxFactory.ParseExpression(source);
        await Assert.That(LayoutHelpers.TryGetChainLink(node, out var lead, out var after, out var name)).IsTrue();
        await Assert.That(lead.Text).IsEqualTo(expectedLead);
        await Assert.That(after.Text).IsEqualTo(".");
        await Assert.That(name.Text).IsEqualTo("B");
    }

    /// <summary>Verifies expressions without a member binding have no chain-link tokens.</summary>
    /// <param name="source">The non-member expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a")]
    [Arguments("a?[0]")]
    public async Task NonMemberChainIsRejectedAsync(string source)
    {
        await Assert.That(LayoutHelpers.TryGetChainLink(SyntaxFactory.ParseExpression(source), out var lead, out var after, out var name)).IsFalse();
        await Assert.That(lead.RawKind).IsEqualTo(0);
        await Assert.That(after.RawKind).IsEqualTo(0);
        await Assert.That(name.RawKind).IsEqualTo(0);
    }

    /// <summary>Verifies every supported control-flow shape exposes its immediate statement.</summary>
    /// <param name="source">The containing statement.</param>
    /// <param name="kind">The node to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (true) ;", SyntaxKind.IfStatement)]
    [Arguments("if (true) { } else ;", SyntaxKind.ElseClause)]
    [Arguments("for (;;) ;", SyntaxKind.ForStatement)]
    [Arguments("foreach (var x in xs) ;", SyntaxKind.ForEachStatement)]
    [Arguments("foreach (var (x, y) in xs) ;", SyntaxKind.ForEachVariableStatement)]
    [Arguments("while (true) ;", SyntaxKind.WhileStatement)]
    [Arguments("do ; while (true);", SyntaxKind.DoStatement)]
    [Arguments("using (x) ;", SyntaxKind.UsingStatement)]
    [Arguments("lock (x) ;", SyntaxKind.LockStatement)]
    [Arguments("fixed (int* p = x) ;", SyntaxKind.FixedStatement)]
    public async Task EmbeddedStatementShapesAsync(string source, SyntaxKind kind)
    {
        var node = SyntaxFactory.ParseStatement(source).DescendantNodesAndSelf().First(candidate => candidate.IsKind(kind));
        await Assert.That(LayoutHelpers.EmbeddedStatementKinds().Contains(kind)).IsTrue();
        await Assert.That(LayoutHelpers.TryGetEmbeddedStatement(node, out var statement)).IsTrue();
        await Assert.That(statement.IsKind(SyntaxKind.EmptyStatement)).IsTrue();
    }

    /// <summary>Verifies a plain block does not expose an embedded control-flow statement.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BlockHasNoEmbeddedStatementAsync()
    {
        await Assert.That(LayoutHelpers.TryGetEmbeddedStatement(SyntaxFactory.Block(), out var statement)).IsFalse();
        await Assert.That(statement).IsNull();
    }

    /// <summary>Verifies all brace-bearing syntax kinds expose real brace tokens.</summary>
    /// <param name="source">The compilation unit containing the brace-bearing node.</param>
    /// <param name="kind">The node kind to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { } }", SyntaxKind.Block)]
    [Arguments("class C { int P { get; } }", SyntaxKind.AccessorList)]
    [Arguments("class C { }", SyntaxKind.ClassDeclaration)]
    [Arguments("struct C { }", SyntaxKind.StructDeclaration)]
    [Arguments("interface C { }", SyntaxKind.InterfaceDeclaration)]
    [Arguments("enum C { }", SyntaxKind.EnumDeclaration)]
    [Arguments("record C { }", SyntaxKind.RecordDeclaration)]
    [Arguments("record struct C { }", SyntaxKind.RecordStructDeclaration)]
    [Arguments("namespace N { }", SyntaxKind.NamespaceDeclaration)]
    [Arguments("class C { void M() { switch (x) { } } }", SyntaxKind.SwitchStatement)]
    [Arguments("class C { object P => x switch { _ => null }; }", SyntaxKind.SwitchExpression)]
    [Arguments("class C { object P = new C { }; }", SyntaxKind.ObjectInitializerExpression)]
    [Arguments("class C { int[] P = new[] { 1 }; }", SyntaxKind.ArrayInitializerExpression)]
    [Arguments("class C { object P = new C { 1 }; }", SyntaxKind.CollectionInitializerExpression)]
    [Arguments("class C { object P = new C { { 1, 2 } }; }", SyntaxKind.ComplexElementInitializerExpression)]
    [Arguments("class C { object P => x with { }; }", SyntaxKind.WithInitializerExpression)]
    [Arguments("class C { object P = new { X = 1 }; }", SyntaxKind.AnonymousObjectCreationExpression)]
    public async Task BraceBearingShapesAsync(string source, SyntaxKind kind)
    {
        var node = SyntaxFactory.ParseCompilationUnit(source).DescendantNodes().First(candidate => candidate.IsKind(kind));
        await Assert.That(LayoutHelpers.BraceBearingKinds().Contains(kind)).IsTrue();
        await Assert.That(LayoutHelpers.TryGetBraces(node, out var open, out var close)).IsTrue();
        await Assert.That(open.IsKind(SyntaxKind.OpenBraceToken)).IsTrue();
        await Assert.That(close.IsKind(SyntaxKind.CloseBraceToken)).IsTrue();
    }

    /// <summary>Verifies absent and missing brace tokens are rejected independently.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingBracesAreRejectedAsync()
    {
        var block = SyntaxFactory.Block();
        var record = (RecordDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("record C;")!;
        await Assert.That(LayoutHelpers.TryGetBraces(SyntaxFactory.IdentifierName("x"), out _, out _)).IsFalse();
        await Assert.That(LayoutHelpers.TryGetBraces(record, out _, out _)).IsFalse();
        await Assert.That(LayoutHelpers.TryGetBraces(record.WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)), out _, out _)).IsFalse();
        await Assert.That(LayoutHelpers.TryGetBraces(block.WithOpenBraceToken(SyntaxFactory.MissingToken(SyntaxKind.OpenBraceToken)), out _, out _)).IsFalse();
        await Assert.That(LayoutHelpers.TryGetBraces(block.WithCloseBraceToken(SyntaxFactory.MissingToken(SyntaxKind.CloseBraceToken)), out _, out _)).IsFalse();
    }

    /// <summary>Parses the single method declaration from a single-type test snippet.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The single method declaration.</returns>
    private static MethodDeclarationSyntax ParseSingleMethod(CompilationUnitSyntax root) =>
        (MethodDeclarationSyntax)((TypeDeclarationSyntax)root.Members[0]).Members[0];

    /// <summary>Parses the method declarations from a single-type test snippet.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The method declarations in source order.</returns>
    private static MethodDeclarationSyntax[] ParseMethods(CompilationUnitSyntax root)
    {
        var members = ((TypeDeclarationSyntax)root.Members[0]).Members;
        var methods = new MethodDeclarationSyntax[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            methods[i] = (MethodDeclarationSyntax)members[i];
        }

        return methods;
    }
}
