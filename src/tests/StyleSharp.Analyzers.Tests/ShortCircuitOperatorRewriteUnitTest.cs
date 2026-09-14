// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared eager-to-short-circuiting boolean operator rewrite.</summary>
public sealed class ShortCircuitOperatorRewriteUnitTest
{
    /// <summary>An eager boolean conjunction.</summary>
    private const string EagerAnd = "a & b";

    /// <summary>A method mixing an eager and a short-circuiting operator.</summary>
    private const string FindSource = "class C { bool M(bool a, bool b, bool c, bool d) => (a & b) & (c || d); }";

    /// <summary>Verifies the eager bitwise operators are recognized and the short-circuiting ones are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsFixableKindMatchesOnlyEagerBooleanOperatorsAsync()
    {
        await Assert.That(ShortCircuitOperatorRewrite.IsFixableKind(ParseBinary(EagerAnd))).IsTrue();
        await Assert.That(ShortCircuitOperatorRewrite.IsFixableKind(ParseBinary("a | b"))).IsTrue();
        await Assert.That(ShortCircuitOperatorRewrite.IsFixableKind(ParseBinary("a && b"))).IsFalse();
        await Assert.That(ShortCircuitOperatorRewrite.IsFixableKind(ParseBinary("a ^ b"))).IsFalse();
    }

    /// <summary>Verifies the rewrite turns eager operators into their short-circuiting form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RewriteProducesShortCircuitingOperatorAsync()
    {
        await Assert.That(ShortCircuitOperatorRewrite.Rewrite(ParseBinary(EagerAnd)).ToString()).IsEqualTo("a && b");
        await Assert.That(ShortCircuitOperatorRewrite.Rewrite(ParseBinary("a | b")).ToString()).IsEqualTo("a || b");
    }

    /// <summary>Verifies the rewrite parenthesizes when a tighter-binding bitwise parent would otherwise capture it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RewriteParenthesizesUnderTighterBitwiseParentAsync()
    {
        // Parses as (a & b) & c: rewriting the inner "a & b" must keep its grouping under the outer "& c".
        var inner = (BinaryExpressionSyntax)ParseBinary("a & b & c").Left;

        var rewritten = ShortCircuitOperatorRewrite.Rewrite(inner);

        await Assert.That(rewritten is ParenthesizedExpressionSyntax).IsTrue();
        await Assert.That(rewritten.ToString()).IsEqualTo("(a && b)");
    }

    /// <summary>Verifies the reported node resolves only when it is an eager boolean operator.</summary>
    /// <param name="reported">The text the diagnostic covers.</param>
    /// <param name="expected">The resolved operator text, or an empty string when nothing resolves.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(EagerAnd, EagerAnd)]
    [Arguments("c || d", "")]
    public async Task FindResolvesOnlyEagerOperatorsAsync(string reported, string expected)
    {
        var tree = CSharpSyntaxTree.ParseText(FindSource);
        var root = await tree.GetRootAsync();

        var found = ShortCircuitOperatorRewrite.Find(root, DiagnosticAt(tree, reported));

        await Assert.That(found?.ToString() ?? string.Empty).IsEqualTo(expected);
    }

    /// <summary>Verifies the batch edit rewrites the reported operator and ignores anything else.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesTheReportedOperatorAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(ShortCircuitOperatorRewrite), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(FindSource));
        var tree = (await document.GetSyntaxTreeAsync())!;
        var editor = await DocumentEditor.CreateAsync(document);

        ShortCircuitOperatorRewrite.FixAll.RegisterBatchEdit(editor, DiagnosticAt(tree, EagerAnd));
        ShortCircuitOperatorRewrite.FixAll.RegisterBatchEdit(editor, DiagnosticAt(tree, "c || d"));

        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo("class C { bool M(bool a, bool b, bool c, bool d) => (a && b) & (c || d); }");
    }

    /// <summary>Creates a diagnostic spanning the last occurrence of some text in <see cref="FindSource"/>.</summary>
    /// <param name="tree">The syntax tree the diagnostic belongs to.</param>
    /// <param name="text">The text the diagnostic covers.</param>
    /// <returns>The diagnostic.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Diagnostic DiagnosticAt(SyntaxTree tree, string text) =>
        Diagnostic.Create(CorrectnessRules.NonShortCircuitGuard, Location.Create(tree, new(FindSource.LastIndexOf(text, StringComparison.Ordinal), text.Length)));

    /// <summary>Parses an expression as a binary expression.</summary>
    /// <param name="expression">The expression source.</param>
    /// <returns>The parsed binary expression.</returns>
    private static BinaryExpressionSyntax ParseBinary(string expression) =>
        (BinaryExpressionSyntax)SyntaxFactory.ParseExpression(expression);
}
