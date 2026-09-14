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

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests stale inlining diagnostics, duplicate fixes, and import placement.</summary>
public class AggressiveInliningCodeFixProviderTests
{
    /// <summary>The source document used for inlining fixes.</summary>
    private const string TestFileName = "Test.cs";

    /// <summary>A header attached to the first existing import.</summary>
    private const string HeaderImportSource = """
        // Header
        using System.Text;

        class C
        {
            int M() => 1;
        }
        """;

    /// <summary>The header moves onto the newly inserted first import.</summary>
    private const string HeaderImportExpected = """
        // Header
        using System.Runtime.CompilerServices;
        using System.Text;

        class C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int M() => 1;
        }
        """;

    /// <summary>A header preceding a conditional namespace declaration.</summary>
    private const string ConditionalNamespaceSource = """
        // Header
        #if true
        namespace Sample;
        #endif
        class C
        {
            int M() => 1;
        }
        """;

    /// <summary>The import follows the header and precedes the namespace directive.</summary>
    private const string ConditionalNamespaceExpected = """
        // Header
        using System.Runtime.CompilerServices;

        #if true
        namespace Sample;
        #endif
        class C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int M() => 1;
        }
        """;

    /// <summary>Aliases and static imports do not import the attribute namespace directly.</summary>
    private const string AliasImportsSource = """
        using System;
        using Alias = System.Runtime.CompilerServices;
        using static System.Math;

        class C
        {
            int M() => 1;
        }
        """;

    /// <summary>The plain attribute namespace import precedes the existing alias.</summary>
    private const string AliasImportsExpected = """
        using System;
        using System.Runtime.CompilerServices;
        using Alias = System.Runtime.CompilerServices;
        using static System.Math;

        class C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int M() => 1;
        }
        """;

    /// <summary>An alias whose target has type syntax instead of name syntax.</summary>
    private const string PredefinedAliasSource = """
        using Count = int;

        class C
        {
            int M() => 1;
        }
        """;

    /// <summary>The new namespace import precedes the predefined-type alias.</summary>
    private const string PredefinedAliasExpected = """
        using System.Runtime.CompilerServices;
        using Count = int;

        class C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int M() => 1;
        }
        """;

    /// <summary>Verifies stale diagnostics and an empty diagnostic batch preserve the document.</summary>
    /// <param name="source">The source whose member no longer qualifies.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("class C { int M() { return 1; } }")]
    public async Task StaleDiagnosticsLeaveDocumentUnchangedAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleInlining", LanguageNames.CSharp).AddDocument(TestFileName, source);
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync())!;
        var location = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.GetLocation() ?? root.GetLocation();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.InlineTrivialForwarders, location);
        using var container = new ContainerConfiguration().WithPart<Psh1410AggressiveInliningCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(Psh1410AggressiveInliningCodeFixProvider.Apply(document, root, [diagnostic])).IsSameReferenceAs(document);
        await Assert.That(Psh1410AggressiveInliningCodeFixProvider.Apply(document, root, [])).IsSameReferenceAs(document);
    }

    /// <summary>Verifies duplicate diagnostics insert one attribute and import while preserving the file header.</summary>
    /// <param name="source">The original forwarder and imports.</param>
    /// <param name="expected">The exact text produced by the fix.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(HeaderImportSource, HeaderImportExpected)]
    [Arguments(ConditionalNamespaceSource, ConditionalNamespaceExpected)]
    [Arguments(AliasImportsSource, AliasImportsExpected)]
    [Arguments(PredefinedAliasSource, PredefinedAliasExpected)]
    public async Task DuplicateDiagnosticsPreserveHeaderAndImportOrderAsync(string source, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("InliningImports", LanguageNames.CSharp).AddDocument(TestFileName, source);
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.InlineTrivialForwarders, member.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1410AggressiveInliningCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            nameof(Psh1410AggressiveInliningCodeFixProvider),
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics([diagnostic, diagnostic]),
            CancellationToken.None);
        var action = await provider.GetFixAllProvider()!.GetFixAsync(context);
        var operations = await action!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var direct = Psh1410AggressiveInliningCodeFixProvider.Apply(document, root, [diagnostic, diagnostic]);
        var directRoot = (await direct.GetSyntaxRootAsync())!;
        await Assert.That(directRoot.DescendantNodes().OfType<AttributeSyntax>().Count()).IsEqualTo(1);
    }

    /// <summary>Verifies an unindented member can receive an attribute.</summary>
    /// <param name="source">The unindented or tightly packed declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C{int M()=>1;}")]
    [Arguments("class C\n{\n// Member\nint M()=>1;\n}")]
    public async Task MemberWithoutIndentationReceivesAttributeAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("InliningTrivia", LanguageNames.CSharp).AddDocument(TestFileName, source);
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.InlineTrivialForwarders, member.Identifier.GetLocation());
        var changed = Psh1410AggressiveInliningCodeFixProvider.Apply(document, root, [diagnostic]);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var attribute = changedRoot.DescendantNodes().OfType<AttributeSyntax>().Single();
        await Assert.That(attribute.ToString()).IsEqualTo("MethodImpl(MethodImplOptions.AggressiveInlining)");
    }

    /// <summary>Verifies Fix All respects directives among the imports.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FixAllLeavesConditionalImportsUnchangedAsync()
    {
        const string Source = "#if true\nusing System;\n#endif\nclass C { int M() => 1; }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ConditionalImports", LanguageNames.CSharp).AddDocument(TestFileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.InlineTrivialForwarders, member.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1410AggressiveInliningCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            nameof(Psh1410AggressiveInliningCodeFixProvider),
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics([diagnostic]),
            CancellationToken.None);
        var action = await provider.GetFixAllProvider()!.GetFixAsync(context);
        var operations = await action!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(Source);
    }

    /// <summary>Supplies the selected diagnostics to a Fix All request.</summary>
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
