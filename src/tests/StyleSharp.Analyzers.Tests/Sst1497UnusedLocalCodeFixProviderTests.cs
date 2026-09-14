// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests unused-local edits that must preserve evaluation or reject stale diagnostics.</summary>
public class Sst1497UnusedLocalCodeFixProviderTests
{
    /// <summary>Checks unsupported declarations and unpreservable expressions produce no action or edit.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("return;", "return")]
    [Arguments("return;", "C")]
    [Arguments("for (int unused = 0; ; ) {}", "unused")]
    [Arguments("int unused = F(), other = 1;", "unused")]
    [Arguments("System.Span<int> unused = stackalloc int[1];", "unused")]
    [Arguments("System.Span<int> unused = stackalloc[] { 1 };", "unused")]
    [Arguments("System.Span<int> unused = default; unused = stackalloc int[1];", "unused")]
    [Arguments("object unused = missing.Value;", "unused")]
    [Arguments("object unused = (FVoid());", "unused")]
    public async Task UnsafeRemovalIsRejectedAsync(string body, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("UnusedLocal", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("Test.cs", $"class C {{ void M() {{ {body} }} int F() => 1; void FVoid() {{}} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST1497", target);
        using var container = new ContainerConfiguration().WithPart<Sst1497UnusedLocalCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(Sst1497UnusedLocalCodeFixProvider.Apply(document, root, model, diagnostic, CancellationToken.None)).IsSameReferenceAs(document);
    }

    /// <summary>Checks unary side effects and unrelated writes survive removing a local.</summary>
    /// <param name="body">The original body.</param>
    /// <param name="expected">The body after removing the unused local.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int unused = ++value;", "++value;")]
    [Arguments("int unused = --value;", "--value;")]
    [Arguments("int unused = value++;", "value++;")]
    [Arguments("int unused = value--;", "value--;")]
    [Arguments("int unused = -F();", "_ = -F();")]
    [Arguments("object unused = F()!;", "_ = F()!;")]
    [Arguments("int unused, other = 1; Use(other);", "int other = 1; Use(other);")]
    [Arguments("int unused; unused = F();", "F();")]
    [Arguments("int unused = 0; other.unused = 1;", "other.unused = 1;")]
    [Arguments("int unused = 0; void Local(int unused) { unused = F(); }", "void Local(int unused) { unused = F(); }")]
    public async Task RemovalPreservesRequiredEvaluationAsync(string body, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var source = $"class C {{ int unused; void M(int value, C other) {{ {body} }} int F() => 1; void Use(int x) {{}} }}";
        var document = workspace.AddProject("UnusedEvaluation", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var variable = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .First(static node => node.Identifier.ValueText == "unused" && node.Parent?.Parent is LocalDeclarationStatementSyntax);
        var diagnostic = Diagnostic.Create(MaintainabilityRules.UnusedLocal, variable.Identifier.GetLocation(), "unused");
        var changed = Sst1497UnusedLocalCodeFixProvider.Apply(document, root, model, diagnostic, CancellationToken.None);
        var expectedRoot = SyntaxFactory.ParseCompilationUnit($"class C {{ int unused; void M(int value, C other) {{ {expected} }} int F() => 1; void Use(int x) {{}} }}");
        await Assert.That(SyntaxFactory.AreEquivalent((await changed.GetSyntaxRootAsync())!, expectedRoot)).IsTrue();
    }
}
