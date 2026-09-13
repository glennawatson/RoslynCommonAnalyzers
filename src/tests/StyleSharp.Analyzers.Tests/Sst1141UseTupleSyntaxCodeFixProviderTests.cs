// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests tuple fixes for stale locations and nested tuple identity.</summary>
public class Sst1141UseTupleSyntaxCodeFixProviderTests
{
    /// <summary>Checks a diagnostic outside a generic name produces no individual or batch edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonGenericLocationIsUnchangedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleTuple", LanguageNames.CSharp).AddDocument("Test.cs", "class C { int value; }");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST1141", "int");
        using var container = new ContainerConfiguration().WithPart<Sst1141UseTupleSyntaxCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            nameof(Sst1141UseTupleSyntaxCodeFixProvider),
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics([diagnostic]),
            CancellationToken.None);
        var action = await provider.GetFixAllProvider()!.GetFixAsync(context);
        var operations = await action!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks only framework tuple arguments are recursively rewritten by an individual action.</summary>
    /// <param name="type">The explicit tuple type.</param>
    /// <param name="expected">The rewritten tuple type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.ValueTuple<int, System.ValueTuple<string, int>>", "(int, (string, int))")]
    [Arguments("System.ValueTuple<int, Foreign.ValueTuple<string, int>>", "(int, Foreign.ValueTuple<string, int>)")]
    [Arguments("System.ValueTuple<int, System.ValueTuple<string>>", "(int, System.ValueTuple<string>)")]
    [Arguments("System.ValueTuple<int, System.Collections.Generic.List<string>>", "(int, System.Collections.Generic.List<string>)")]
    public async Task NestedTupleIdentityControlsRecursionAsync(string type, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("NestedTuple", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("Test.cs", $"class C {{ {type} value; }} namespace Foreign {{ struct ValueTuple<T, U> {{ }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var generic = root.DescendantNodes().OfType<GenericNameSyntax>().First();
        var diagnostic = Diagnostic.Create(ReadabilityRules.UseTupleSyntax, generic.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1141UseTupleSyntaxCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.DescendantNodes().OfType<FieldDeclarationSyntax>().Single().Declaration.Type.ToString()).IsEqualTo(expected);
        var direct = Sst1141UseTupleSyntaxCodeFixProvider.Replace(document, root, generic);
        var directRoot = (await direct.GetSyntaxRootAsync())!;
        await Assert.That(directRoot.DescendantNodes().OfType<FieldDeclarationSyntax>().Single().Declaration.Type).IsTypeOf<TupleTypeSyntax>();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(directRoot.ToFullString());
    }

    /// <summary>Checks duplicate diagnostics select one replacement for the same tuple span.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DuplicateTupleDiagnosticsProduceOneReplacementAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("DuplicateTuple", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("DuplicateTuple.cs", "class C { System.ValueTuple<int, string> value; }");
        var root = (await document.GetSyntaxRootAsync())!;
        var generic = root.DescendantNodes().OfType<GenericNameSyntax>().Single();
        var diagnostic = Diagnostic.Create(ReadabilityRules.UseTupleSyntax, generic.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1141UseTupleSyntaxCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            nameof(Sst1141UseTupleSyntaxCodeFixProvider),
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics([diagnostic, diagnostic]),
            CancellationToken.None);
        var action = await provider.GetFixAllProvider()!.GetFixAsync(context);
        var operations = await action!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.DescendantNodes().OfType<FieldDeclarationSyntax>().Single().Declaration.Type.ToString()).IsEqualTo("(int, string)");
    }

    /// <summary>Supplies exactly the selected diagnostics to a Fix All request.</summary>
    /// <param name="diagnostics">The diagnostics returned for the document.</param>
    private sealed class SelectedDiagnostics(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Diagnostic>>([]);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
