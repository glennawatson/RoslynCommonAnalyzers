// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.CodeFixes;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests stale else-collapse diagnostics and preservation of comment trivia.</summary>
public class CollapseElseIntoElseIfCodeFixProviderTests
{
    /// <summary>The diagnostic handled by the provider.</summary>
    private const string DiagnosticId = "SST1465";

    /// <summary>The source document name.</summary>
    private const string FileName = "Test.cs";

    /// <summary>Checks changed bodies and directive boundaries prevent registration and editing.</summary>
    /// <param name="body">The current method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("return;", "return")]
    [Arguments("if (flag) {} else if (flag) {}", "else")]
    [Arguments("if (flag) {} else {}", "else")]
    [Arguments("if (flag) {} else { return; }", "else")]
    [Arguments("if (flag) {} else { if (flag) {} return; }", "else")]
    [Arguments("if (flag) {} else\n#region Branch\n{ if (flag) {} }\n#endregion\n", "else")]
    [Arguments("if (flag) {} else {\n#region Branch\nif (flag) {}\n#endregion\n}", "else")]
    [Arguments("if (flag) {} else { if (flag) {}\n#region Branch\n}\n#endregion\n", "else")]
    public async Task InapplicableElseIsLeftUnchangedAsync(string body, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ElseCollapse", LanguageNames.CSharp).AddDocument(FileName, $"class C {{ void M(bool flag) {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, target);
        using var container = new ContainerConfiguration().WithPart<Sst1465CollapseElseIntoElseIfCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(0);
        await Assert.That(ReplaceNodeCodeFix.Apply(document, root, diagnostic, Sst1465CollapseElseIntoElseIfCodeFixProvider.TryRewrite)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst1465CollapseElseIntoElseIfCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks comments attached to removed braces remain attached to the hoisted if.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task CommentsAndWhitespaceSurviveCollapseAsync()
    {
        using var workspace = new AdhocWorkspace();
        const string Source = "class C { void M(bool flag) { if (flag) {} else { if (flag) {} } } }";
        var document = workspace.AddProject("ElseComments", LanguageNames.CSharp).AddDocument(FileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var original = root.DescendantNodes().OfType<ElseClauseSyntax>().Single();
        var block = (BlockSyntax)original.Statement;
        var inner = (IfStatementSyntax)block.Statements[0];
        var trailing = SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("  "), SyntaxFactory.Comment("/*inner*/"), SyntaxFactory.LineFeed, SyntaxFactory.LineFeed, SyntaxFactory.Whitespace("    "));
        var changedBlock = block.WithStatements(SyntaxFactory.SingletonList<StatementSyntax>(inner.WithTrailingTrivia(trailing)))
            .WithOpenBraceToken(block.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Comment("//leading")))
            .WithCloseBraceToken(block.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Comment("/*closing*/"), SyntaxFactory.LineFeed).WithTrailingTrivia(SyntaxFactory.LineFeed));
        root = root.ReplaceNode(original, original.WithStatement(changedBlock));
        document = document.WithSyntaxRoot(root);
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "else");
        var changed = ReplaceNodeCodeFix.Apply(document, root, diagnostic, Sst1465CollapseElseIntoElseIfCodeFixProvider.TryRewrite);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var collapsed = changedRoot.DescendantNodes().OfType<ElseClauseSyntax>().Single();
        await Assert.That(collapsed.Statement).IsTypeOf<IfStatementSyntax>();
        await Assert.That(collapsed.ToFullString()).Contains("//leading\r\n");
        await Assert.That(collapsed.ToFullString()).Contains("/*inner*/");
        await Assert.That(collapsed.ToFullString()).Contains("/*closing*/");
        await Assert.That(collapsed.ToFullString()).DoesNotContain("\n\n");
    }

    /// <summary>Checks duplicate batch diagnostics do not collapse an already rewritten else clause twice.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task DuplicateBatchDiagnosticIsIdempotentAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("DuplicateElse", LanguageNames.CSharp)
            .AddDocument(FileName, "class C { void M(bool flag) { if (flag) {} else { if (flag) {} } } }");
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<ElseClauseSyntax>().Single();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.CollapseElseIntoElseIf, clause.GetLocation());
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst1465CollapseElseIntoElseIfCodeFixProvider>(editor, diagnostic);
        BatchEditRegistration.Register<Sst1465CollapseElseIntoElseIfCodeFixProvider>(editor, diagnostic);
        var changedClause = editor.GetChangedRoot().DescendantNodes().OfType<ElseClauseSyntax>().Single();
        await Assert.That(changedClause.Statement).IsTypeOf<IfStatementSyntax>();
    }

    /// <summary>Checks leading comments and statement attributes survive a syntax-only collapse.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task AttributedInnerIfKeepsItsAttributesAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ElseAttributes", LanguageNames.CSharp).AddDocument(FileName, "class C { void M(bool flag) { if (flag) {} else { /*keep*/ [Marker] if (flag) {} } } }");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "else");
        var changed = ReplaceNodeCodeFix.Apply(document, root, diagnostic, Sst1465CollapseElseIntoElseIfCodeFixProvider.TryRewrite);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var collapsed = changedRoot.DescendantNodes().OfType<ElseClauseSyntax>().Single();
        var inner = (IfStatementSyntax)collapsed.Statement;
        await Assert.That(inner.AttributeLists.Count).IsEqualTo(1);
        await Assert.That(inner.ToFullString()).Contains("/*keep*/");
    }
}
