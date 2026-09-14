// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests ordering fixes for stale diagnostics and member boundaries.</summary>
public sealed class MemberOrderingCodeFixProviderTests
{
    /// <summary>The source document receiving an ordering fix.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Checks diagnostics outside a type member do not register a move.</summary>
    /// <param name="source">The document containing the stale diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System;")]
    [Arguments("class C { }")]
    [Arguments("namespace N { class C { } }")]
    public async Task DiagnosticOutsideMemberHasNoActionAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(OrderingRules.OrderByKind, root.GetFirstToken().GetLocation());
        using var container = new ContainerConfiguration().WithPart<MemberOrderingCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Checks stale moves leave directives, unsupported members and already ordered members intact.</summary>
    /// <param name="source">The source whose final member was reported.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { }\n#region Keep\nint value;\n#endregion\n}")]
    [Arguments("class C { int value; void M() { } }")]
    [Arguments("class C { void M() { } }")]
    [Arguments("class C { int }")]
    [Arguments("class C : I { void I.M() { } } interface I { void M(); }")]
    public async Task StaleMoveReturnsOriginalDocumentAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().Members.Last();
        var changed = await MemberOrderingCodeFixProvider.MoveAsync(document, member, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(document);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(source);
    }

    /// <summary>Checks rank scans skip incomplete members and classify a resolved union marker.</summary>
    /// <param name="source">The source containing an out-of-order member.</param>
    /// <param name="expected">The source after the final member is moved.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(
        "class C : I { void I.M() { } void M() { } int value; } interface I { void M(); }",
        "class C : I { void I.M() { } int value; void M() { } } interface I { void M(); }")]
    [Arguments(
        "class C { class U : System.Runtime.CompilerServices.IUnion { } class D { } } namespace System.Runtime.CompilerServices { interface IUnion { } }",
        "class C { class D { } class U : System.Runtime.CompilerServices.IUnion { } } namespace System.Runtime.CompilerServices { interface IUnion { } }")]
    public async Task MoveUsesAvailableMemberRanksAsync(string source, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First().Members.Last();
        var changed = await MemberOrderingCodeFixProvider.MoveAsync(document, member, CancellationToken.None);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
    }
}
