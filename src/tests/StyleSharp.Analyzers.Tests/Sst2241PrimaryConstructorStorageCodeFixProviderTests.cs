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
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests primary-constructor rewrites against stale diagnostics and alternate storage shapes.</summary>
public class Sst2241PrimaryConstructorStorageCodeFixProviderTests
{
    /// <summary>Verifies parameter documentation keeps the containing type's indentation with every newline convention.</summary>
    /// <param name="newline">The line separator in the original document.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    [Arguments("\r")]
    public async Task ParameterDocumentationPreservesLineEndingsAsync(string newline)
    {
        var source = string.Join(
            newline,
            "namespace N {",
            "    // A type",
            "    class C",
            "    {",
            "        int field;",
            "        /// <summary>Constructs storage.</summary>",
            "        /// <param name=\"value\">The stored value.</param>",
            "        C(int value) { field = value; }",
            "    }",
            "}");
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Docs.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var constructor = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UsePrimaryConstructorStorage, constructor.Identifier.GetLocation());
        var changed = Sst2241PrimaryConstructorStorageCodeFixProvider.Apply(document, root, diagnostic);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var type = changedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        await Assert.That(type.GetLeadingTrivia().ToFullString()).Contains($"    /// <param name=\"value\">The stored value.</param>{newline}");
        await Assert.That(type.ParameterList!.Parameters.Single().Identifier.ValueText).IsEqualTo("value");
        await Assert.That(type.Members.OfType<ConstructorDeclarationSyntax>()).IsEmpty();
    }

    /// <summary>Verifies unsupported constructor bodies and storage targets receive no edit.</summary>
    /// <param name="source">The syntax associated with a stale constructor diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("class C { int field; C(int value) => field = value; }")]
    [Arguments("class C { C(int value) { } }")]
    [Arguments("class C { C(int value) { M(); } void M() { } }")]
    [Arguments("class C { int field; C(int value) { field += value; } }")]
    [Arguments("class C { int field; C(int value) { field = 1; } }")]
    [Arguments("class C { int[] field; C(int value) { field[0] = value; } }")]
    [Arguments("class C { C(int value) { missing = value; } }")]
    [Arguments("class C { int field = 1; C(int value) { field = value; } }")]
    [Arguments("class C { int P { get; } = 1; C(int value) { P = value; } }")]
    [Arguments("class C { int P => 1; C(int value) { P = value; } }")]
    [Arguments("class C { int P { get { return 1; } } C(int value) { P = value; } }")]
    [Arguments("class C { int P { get => 1; } C(int value) { P = value; } }")]
    [Arguments("class C { int field; C(int value) { field = value; field = value; } }")]
    [Arguments("class C { int field; C(int value) : this(value, value) { field = value; } }")]
    [Arguments("class C { int field; C(int value) : base(value) { field = value; } }")]
    [Arguments("class C {\n#region Storage\nint field;\n#endregion\nC(int value) { field = value; } }")]
    public async Task UnsupportedStorageHasNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Sst2241PrimaryConstructorStorageCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UsePrimaryConstructorStorage, (target?.Identifier ?? root.GetFirstToken()).GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(Sst2241PrimaryConstructorStorageCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies every body-scope declaration that shadows a promoted parameter blocks the fix.</summary>
    /// <param name="member">The member declaring a conflicting name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void M(int value) { }")]
    [Arguments("void M() { int value = 0; }")]
    [Arguments("void M() { foreach (var value in new int[0]) { } }")]
    [Arguments("void M() { try { } catch (System.Exception value) { } }")]
    [Arguments("void M(object item) { if (item is int value) { } }")]
    [Arguments("void M() { void value() { } }")]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Task BodyScopeCollisionHasNoFixAsync(string member) =>
        UnsupportedStorageHasNoFixAsync($$"""class C { int field; C(int value) { field = value; } {{member}} }""");

    /// <summary>Verifies supported storage forms preserve unassigned members and constructor base calls.</summary>
    /// <param name="source">The constructor to rewrite.</param>
    /// <param name="expected">The expected primary-constructor syntax.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("struct C { int field; public C(int value) { this.field = value; } }", "struct C(int value) { int field = value; }")]
    [Arguments(
        "class C { int first, second; int P { get; } int Q { get; } C(int a, int b) { first = a; P = b; } }",
        "class C(int a, int b) { int first = a, second; int P { get; } = b; int Q { get; } }")]
    [Arguments("class C { int field; C(int value) : base() { field = value; } }", "class C(int value) { int field = value; }")]
    [Arguments("class C { int field; C() { field = Default; } }", "class C() { int field = Default; }")]
    [Arguments("class C<T>:B { int field; C(int value) : base(value) { field = value; } }", "class C<T>(int value) : B(value) { int field = value; }")]
    [Arguments("class C<T>where T:class { T field; C(T value) { field = value; } }", "class C<T>(T value) where T:class { T field = value; }")]
    [Arguments("class C:B { int field; C(int value) : base(value) { field = value; } }", "class C(int value) : B(value) { int field = value; }")]
    [Arguments("class C(int unused):B(0) { int field; C(int value) : base(value) { field = value; } }", "class C(int value) : B(value) { int field = value; }")]
    [Arguments(
        "class C { int field; C(int value) { field = value; } class Nested { int value; } void M() { try { } catch (System.Exception) { } } }",
        "class C(int value) { int field = value; class Nested { int value; } void M() { try { } catch (System.Exception) { } } }")]
    public async Task StorageRewritePreservesOtherMembersAsync(string source, string expected)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Sst2241PrimaryConstructorStorageCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var constructor = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().First();
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UsePrimaryConstructorStorage, constructor.Identifier.GetLocation());
        var changed = Sst2241PrimaryConstructorStorageCodeFixProvider.Apply(document, root, diagnostic);
        var expectedRoot = (await CSharpSyntaxTree.ParseText(expected).GetRootAsync()).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applied = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await applied.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
    }
}
