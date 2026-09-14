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
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests nested-member moves when diagnostic information is stale or members carry adjacent comments.</summary>
public class Sst1498PrivateMemberUsedOnlyByNestedTypeCodeFixProviderTests
{
    /// <summary>The diagnostic used to select the member to relocate.</summary>
    private static readonly DiagnosticDescriptor Descriptor = new("SST1498", "Test", "Test", "Tests", DiagnosticSeverity.Warning, true);

    /// <summary>Verifies members no longer owned by one nested type cannot be moved by individual or batch fixes.</summary>
    /// <param name="source">The updated source containing a method named Helper.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("partial class Outer { private static int Helper() => 1; class Inner { int M() => Helper(); } }")]
    [Arguments("class Outer { private static int Helper() => 1; private static int Other() => 2; class Inner { int M() => Other(); } }")]
    [Arguments("class Outer { private static int Helper() => 1; int M() => Helper(); class Inner { } }")]
    public async Task StaleMemberDiagnosticDoesNotMoveAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Sst1498PrivateMemberUsedOnlyByNestedTypeCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "Helper");
        var diagnostic = Diagnostic.Create(Descriptor, member.Identifier.GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        await ((IAsyncBatchableCodeFix)provider).RegisterEditsAsync(editor, diagnostic, CancellationToken.None);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies constructors do not block a move and the following member keeps its comment.</summary>
    /// <param name="separator">The trivia before the nested type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\n/* keep */ ")]
    [Arguments(" ")]
    public async Task MovePreservesFollowingMemberTriviaAsync(string separator)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Sst1498PrivateMemberUsedOnlyByNestedTypeCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var source = $"class Outer {{ private static int Helper() => 1;{separator}class Inner {{ int M() => Helper(); }} public Outer() {{ }} }}";
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var diagnostic = Diagnostic.Create(Descriptor, member.Identifier.GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var expected = await CSharpSyntaxTree.ParseText($"class Outer {{ {separator}class Inner {{ int M() => Helper(); private static int Helper() => 1; }} public Outer() {{ }} }}").GetRootAsync();
        await Assert.That(changedRoot.NormalizeWhitespace().ToFullString()).IsEqualTo(expected.NormalizeWhitespace().ToFullString());
    }
}
