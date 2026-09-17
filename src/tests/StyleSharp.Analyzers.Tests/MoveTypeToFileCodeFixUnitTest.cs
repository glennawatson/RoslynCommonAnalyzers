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
using VerifyMove = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.FileTypeNamespaceAnalyzer,
    StyleSharp.Analyzers.Sst1402MoveTypeToFileCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1402 move-type-to-file code fix.</summary>
public class MoveTypeToFileCodeFixUnitTest
{
    /// <summary>The document count in each project after extracting one type.</summary>
    private const int ExtractedDocumentCount = 2;

    /// <summary>The folder retained by both linked target projects.</summary>
    private const string ModelFolder = "Models";

    /// <summary>The global analyzer config selecting the backtick-arity (metadata) generic convention.</summary>
    private const string MetadataConfig = """
        is_global = true
        stylesharp.file_naming_convention = metadata

        """;

    /// <summary>The file named for the first type, which the extra types are moved out of.</summary>
    private const string FirstTypeFileName = "First.cs";

    /// <summary>The file the moved <c>Second</c> type is expected to land in.</summary>
    private const string SecondTypeFileName = "Second.cs";

    /// <summary>What the first type's file holds once every extra type has been moved out.</summary>
    private const string FirstTypeOnlySource = """
        public class First
        {
        }
        """;

    /// <summary>Checks extraction preserves line endings while removing blank lines at removal seams.</summary>
    /// <param name="source">The original document.</param>
    /// <param name="original">The expected remaining document.</param>
    /// <param name="extracted">The expected moved document.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class First {} class Second {}", "class First {} ", "class Second {}")]
    [Arguments("class Second {}", "", "class Second {}")]
    [Arguments("class First {}\r\n\r\nclass Second {}\r\n", "class First {}\r\n", "class Second {}\r\n")]
    [Arguments("\n\nclass First {}\n\n\nclass Second {}\n\n", "class First {}\n", "class Second {}\n")]
    [Arguments(
        "namespace N\n{ \t\n \t\nclass First {}\n\nclass Second\n{\n\nint value;\n\n}\n\n}\n",
        "namespace N\n{ \t\nclass First {}\n}\n",
        "namespace N\n{ \t\nclass Second\n{\nint value;\n}\n}\n")]
    [Arguments(
        "// Header\nusing System;\n\nnamespace N;\n\nclass First {}\n\nclass Second {}\n",
        "// Header\nusing System;\n\nnamespace N;\n\nclass First {}\n",
        "// Header\nusing System;\n\nnamespace N;\n\nclass Second {}\n")]
    [Arguments(
        "class First {}\n\nclass Second\n{\n    int first;\n\n    int second;\n\n    }\n",
        "class First {}\n",
        "class Second\n{\n    int first;\n\n    int second;\n    }\n")]
    public async Task ExtractionPreservesTextConventionsAsync(string source, string original, string extracted)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FirstTypeFileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(document, type, SecondTypeFileName, CancellationToken.None);
        var moved = changed.GetProject(document.Project.Id)!.Documents.Single(static item => item.Name == SecondTypeFileName);
        await Assert.That((await changed.GetDocument(document.Id)!.GetTextAsync()).ToString()).IsEqualTo(original);
        await Assert.That((await moved.GetTextAsync()).ToString()).IsEqualTo(extracted);
    }

    /// <summary>Checks empty move requests leave the original solution unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyMoveRequestIsIgnoredAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FirstTypeFileName, FirstTypeOnlySource);
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAllAsync(document, [], CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(document.Project.Solution);
    }

    /// <summary>Checks extraction updates every linked project and preserves document folders.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LinkedProjectsReceiveTheExtractedTypeAsync()
    {
        using var workspace = new AdhocWorkspace();
        var firstProject = workspace.AddProject("FirstTarget", LanguageNames.CSharp);
        var secondProject = workspace.AddProject("SecondTarget", LanguageNames.CSharp);
        var firstId = DocumentId.CreateNewId(firstProject.Id);
        var secondId = DocumentId.CreateNewId(secondProject.Id);
        var source = SourceText.From("class First {}\nclass Second {}\n");
        var solution = workspace.CurrentSolution
            .AddDocument(firstId, FirstTypeFileName, source, [ModelFolder], "/project/Models/First.cs")
            .AddDocument(secondId, FirstTypeFileName, source, [ModelFolder], "/project/Models/First.cs");
        var document = solution.GetDocument(firstId)!;
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(document, type, SecondTypeFileName, CancellationToken.None);
        foreach (var project in changed.Projects)
        {
            await Assert.That(project.DocumentIds.Count).IsEqualTo(ExtractedDocumentCount);
            var original = project.Documents.Single(static item => item.Name == FirstTypeFileName);
            var moved = project.Documents.Single(static item => item.Name == SecondTypeFileName);
            await Assert.That((await original.GetTextAsync()).ToString()).IsEqualTo("class First {}\n");
            await Assert.That((await moved.GetTextAsync()).ToString()).IsEqualTo("class Second {}\n");
            await Assert.That(moved.Folders).IsEquivalentTo([ModelFolder]);
            await Assert.That(moved.FilePath).IsEqualTo(Path.GetFullPath("/project/Models/Second.cs"));
        }
    }

    /// <summary>Checks conditional files and diagnostics outside a type do not register a move.</summary>
    /// <param name="source">The document receiving the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("#if ACTIVE\nclass First {}\n#endif\nclass Second {}")]
    [Arguments("namespace N { class First {} }")]
    public async Task UnmovableDiagnosticDoesNotRegisterAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FirstTypeFileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(MaintainabilityRules.SingleType, root.GetFirstToken().GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1402MoveTypeToFileCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Checks Fix All leaves a stale namespace diagnostic unchanged when no type can be resolved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FixAllIgnoresStaleNamespaceDiagnosticAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FirstTypeFileName, "namespace N { class First {} }");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(MaintainabilityRules.SingleType, root.GetFirstToken().GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1402MoveTypeToFileCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            nameof(Sst1402MoveTypeToFileCodeFixProvider),
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics([diagnostic]),
            CancellationToken.None);
        var action = (await provider.GetFixAllProvider()!.GetFixAsync(context))!;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution ?? document.Project.Solution;
        await Assert.That(changed).IsSameReferenceAs(document.Project.Solution);
    }

    /// <summary>Verifies a second top-level type is moved to its own file.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecondTypeMovedToOwnFileAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add((FirstTypeFileName, """
            public class First
            {
            }

            public class {|SST1402:Second|}
            {
            }
            """));
        test.FixedState.Sources.Add((FirstTypeFileName, FirstTypeOnlySource));
        test.FixedState.Sources.Add((SecondTypeFileName, """
            public class Second
            {
            }
            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the moved type keeps its enclosing namespace and the new file is named for the type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeInNamespaceKeepsNamespaceAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add((FirstTypeFileName, """
            namespace N
            {
                public class First
                {
                }

                public class {|SST1402:Second|}
                {
                }
            }
            """));
        test.FixedState.Sources.Add((FirstTypeFileName, """
            namespace N
            {
                public class First
                {
                }
            }
            """));
        test.FixedState.Sources.Add((SecondTypeFileName, """
            namespace N
            {
                public class Second
                {
                }
            }
            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a moved generic type uses the brace convention for its file name by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericTypeUsesBraceConventionAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add((FirstTypeFileName, """
            public class First
            {
            }

            public class {|SST1402:Widget|}<T>
            {
            }
            """));
        test.FixedState.Sources.Add((FirstTypeFileName, FirstTypeOnlySource));
        test.FixedState.Sources.Add(("Widget{T}.cs", """
            public class Widget<T>
            {
            }
            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the metadata convention names a moved generic type's file with backtick arity.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericTypeUsesMetadataConventionAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add((FirstTypeFileName, """
            public class First
            {
            }

            public class {|SST1402:Widget|}<TKey, TValue>
            {
            }
            """));
        test.FixedState.Sources.Add((FirstTypeFileName, FirstTypeOnlySource));
        test.FixedState.Sources.Add(("Widget`2.cs", """
            public class Widget<TKey, TValue>
            {
            }
            """));
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", MetadataConfig));
        test.FixedState.AnalyzerConfigFiles.Add(("/.globalconfig", MetadataConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies Fix All extracts every flagged type from a file in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllMovesEveryExtraTypeAsync()
    {
        var test = new VerifyMove.Test();
        test.TestState.Sources.Add((FirstTypeFileName, """
            public class First
            {
            }

            public class {|SST1402:Second|}
            {
            }

            public class {|SST1402:Third|}
            {
            }
            """));
        test.FixedState.Sources.Add((FirstTypeFileName, FirstTypeOnlySource));
        test.FixedState.Sources.Add((SecondTypeFileName, """
            public class Second
            {
            }
            """));
        test.FixedState.Sources.Add(("Third.cs", """
            public class Third
            {
            }
            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Supplies a selected set of diagnostics for a document Fix All request.</summary>
    /// <param name="diagnostics">The diagnostics returned to the provider.</param>
    private sealed class SelectedDiagnostics(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
