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
using Microsoft.CodeAnalysis.Text;
using VerifyRename = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1649FileNameAnalyzer,
    StyleSharp.Analyzers.Sst1649FileNameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1649 rename-file code fix.</summary>
public class FileNameCodeFixUnitTest
{
    /// <summary>The name of the source file that does not match the type it declares.</summary>
    private const string MismatchedFileName = "Other.cs";

    /// <summary>The logical folder retained by every renamed linked document.</summary>
    private const string ModelFolder = "Models";

    /// <summary>The global analyzer config selecting the backtick-arity (metadata) generic convention.</summary>
    private const string MetadataConfig = """
        is_global = true
        stylesharp.file_naming_convention = metadata

        """;

    /// <summary>Verifies the file is renamed to match its first type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileRenamedToMatchTypeAsync()
    {
        var test = new VerifyRename.Test();
        test.TestState.Sources.Add((MismatchedFileName, "public class {|SST1649:Widget|} { }"));
        test.FixedState.Sources.Add(("Widget.cs", "public class Widget { }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a generic type renames the file using the brace convention by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericFileRenamedWithBraceConventionAsync()
    {
        var test = new VerifyRename.Test();
        test.TestState.Sources.Add((MismatchedFileName, "public class {|SST1649:Widget|}<T> { }"));
        test.FixedState.Sources.Add(("Widget{T}.cs", "public class Widget<T> { }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the metadata convention renames a generic type's file with backtick arity.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericFileRenamedWithMetadataConventionAsync()
    {
        var test = new VerifyRename.Test();
        test.TestState.Sources.Add((MismatchedFileName, "public class {|SST1649:Widget|}<TKey, TValue> { }"));
        test.FixedState.Sources.Add(("Widget`2.cs", "public class Widget<TKey, TValue> { }"));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", MetadataConfig));
        test.FixedState.AnalyzerConfigFiles.Add(("/.globalconfig", MetadataConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies Fix All renames every misnamed file in the scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRenamesEveryFileAsync()
    {
        var test = new VerifyRename.Test();
        test.TestState.Sources.Add(("WrongA.cs", "public class {|SST1649:Apple|} { }"));
        test.TestState.Sources.Add(("WrongB.cs", "public class {|SST1649:Banana|} { }"));
        test.FixedState.Sources.Add(("Apple.cs", "public class Apple { }"));
        test.FixedState.Sources.Add(("Banana.cs", "public class Banana { }"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a rename updates every linked copy while preserving the source and logical folders.</summary>
    /// <param name="fixAll">Whether the rename is invoked through the document Fix All action.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LinkedDocumentsRenamedTogetherAsync(bool fixAll)
    {
        const string Source = "// Preserve the source exactly.\npublic class Widget { }\n";
        using var workspace = new AdhocWorkspace();
        var firstProject = ProjectId.CreateNewId();
        var secondProject = ProjectId.CreateNewId();
        var firstId = DocumentId.CreateNewId(firstProject);
        var secondId = DocumentId.CreateNewId(secondProject);
        var solution = workspace.CurrentSolution
            .AddProject(firstProject, "FirstTarget", "FirstTarget", LanguageNames.CSharp)
            .AddProject(secondProject, "SecondTarget", "SecondTarget", LanguageNames.CSharp)
            .AddDocument(firstId, MismatchedFileName, SourceText.From(Source), [ModelFolder], filePath: "/shared/Other.cs")
            .AddDocument(secondId, MismatchedFileName, SourceText.From(Source), [ModelFolder], filePath: "/shared/Other.cs");
        var document = solution.GetDocument(firstId)!;
        await Assert.That(document.GetLinkedDocumentIds()).Contains(secondId);
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(DocumentationRules.FileNameMatchesType, type.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1649FileNameCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        CodeAction action;
        if (fixAll)
        {
            var context = new FixAllContext(
                document,
                provider,
                FixAllScope.Document,
                nameof(Sst1649FileNameCodeFixProvider),
                provider.FixableDiagnosticIds,
                new SelectedDiagnostics([diagnostic]),
                CancellationToken.None);
            action = (await provider.GetFixAllProvider()!.GetFixAsync(context))!;
        }
        else
        {
            var actions = new List<CodeAction>();
            await provider.RegisterCodeFixesAsync(new(document, diagnostic, (registered, _) => actions.Add(registered), CancellationToken.None));
            await Assert.That(actions.Count).IsEqualTo(1);
            action = actions[0];
        }

        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        await Assert.That(changed.GetDocument(firstId)).IsNull();
        await Assert.That(changed.GetDocument(secondId)).IsNull();
        foreach (var project in changed.Projects)
        {
            var renamed = project.Documents.Single();
            await Assert.That(renamed.Name).IsEqualTo("Widget.cs");
            await Assert.That(renamed.Folders.Single()).IsEqualTo(ModelFolder);
            await Assert.That((await renamed.GetTextAsync()).ToString()).IsEqualTo(Source);
        }
    }

    /// <summary>Verifies a diagnostic on a using directive cannot rename a document through either action path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticOutsideMemberDoesNotRenameFileAsync()
    {
        const string Source = "using System;\nclass Widget { }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(MismatchedFileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var directive = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Single();
        var diagnostic = Diagnostic.Create(DocumentationRules.FileNameMatchesType, directive.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1649FileNameCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            nameof(Sst1649FileNameCodeFixProvider),
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics([diagnostic]),
            CancellationToken.None);
        var fixAll = await provider.GetFixAllProvider()!.GetFixAsync(context);
        var operations = await fixAll!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution.GetDocument(document.Id) ?? document;
        await Assert.That(changed.Name).IsEqualTo(MismatchedFileName);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(Source);
    }

    /// <summary>Supplies the selected diagnostics to a document Fix All request.</summary>
    /// <param name="diagnostics">The diagnostics returned for the document.</param>
    private sealed class SelectedDiagnostics(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
    {
        /// <summary>Returns the diagnostics selected for the document.</summary>
        /// <param name="document">The document being inspected.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The selected diagnostics.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

        /// <summary>Returns no project-level diagnostics.</summary>
        /// <param name="project">The project being inspected.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An empty diagnostic sequence.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>([]);

        /// <summary>Returns the selected document diagnostics.</summary>
        /// <param name="project">The project being inspected.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The selected diagnostics.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
