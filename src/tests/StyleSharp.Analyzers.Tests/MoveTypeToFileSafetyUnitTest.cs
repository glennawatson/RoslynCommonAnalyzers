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
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Checks that extracting a type cannot overwrite another file or break an MSBuild project.</summary>
public class MoveTypeToFileSafetyUnitTest
{
    /// <summary>The shared physical source path in linked projects.</summary>
    private const string SharedSourcePath = "/project/First.cs";

    /// <summary>The source file name.</summary>
    private const string FirstFile = "First.cs";

    /// <summary>The destination file name.</summary>
    private const string SecondFile = "Second.cs";

    /// <summary>The content of a pre-existing destination.</summary>
    private const string ExistingText = "// Existing file";

    /// <summary>The original declarations, which must survive a rejected extraction.</summary>
    private const string Source = "class First {}\nclass Second {}\n";

    /// <summary>Checks source-name collisions and existing destination documents reject the whole move.</summary>
    /// <param name="sourceName">The original file name.</param>
    /// <param name="existingDestination">Whether another document already occupies the destination.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(SecondFile, false)]
    [Arguments(FirstFile, true)]
    public async Task DocumentCollisionLeavesSolutionUnchangedAsync(string sourceName, bool existingDestination)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = project.AddDocument(sourceName, Source);
        if (existingDestination)
        {
            document = document.Project.AddDocument(SecondFile, ExistingText).Project.GetDocument(document.Id)!;
        }

        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(document, type, SecondFile, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(document.Project.Solution);
    }

    /// <summary>Checks two declarations cannot be extracted into the same destination during Fix All.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DuplicateBatchDestinationLeavesSolutionUnchangedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FirstFile, "class First {} class Second {} class Third {}");
        var root = (await document.GetSyntaxRootAsync())!;
        var types = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToArray();
        TypeFileMove[] moves = [new(types[1], SecondFile), new(types[2], SecondFile)];
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAllAsync(document, moves, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(document.Project.Solution);
    }

    /// <summary>Checks a collision in a linked project prevents modifications to either project.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LinkedProjectCollisionLeavesSolutionUnchangedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var firstProject = workspace.AddProject("FirstTarget", LanguageNames.CSharp);
        var secondProject = workspace.AddProject("SecondTarget", LanguageNames.CSharp);
        var firstId = DocumentId.CreateNewId(firstProject.Id);
        var secondId = DocumentId.CreateNewId(secondProject.Id);
        var solution = workspace.CurrentSolution
            .AddDocument(firstId, FirstFile, SourceText.From(Source), filePath: SharedSourcePath)
            .AddDocument(secondId, FirstFile, SourceText.From(Source), filePath: SharedSourcePath)
            .AddDocument(DocumentId.CreateNewId(secondProject.Id), SecondFile, SourceText.From(ExistingText));
        var document = solution.GetDocument(firstId)!;
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(document, type, SecondFile, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(solution);
    }

    /// <summary>Checks a destination path occupied under a different logical name still rejects the move.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PhysicalDocumentPathCollisionLeavesSolutionUnchangedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var id = DocumentId.CreateNewId(project.Id);
        var solution = project.Solution
            .AddDocument(id, FirstFile, SourceText.From(Source), filePath: SharedSourcePath)
            .AddDocument(DocumentId.CreateNewId(project.Id), "Existing.cs", SourceText.From(ExistingText), filePath: "/project/Second.cs");
        var document = solution.GetDocument(id)!;
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(document, type, SecondFile, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(solution);
    }

    /// <summary>Checks an identically named file in another folder does not block a distinct destination.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DifferentFolderDestinationIsAllowedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var id = DocumentId.CreateNewId(project.Id);
        var existingId = DocumentId.CreateNewId(project.Id);
        var solution = project.Solution
            .AddDocument(id, FirstFile, SourceText.From(Source), ["Models"], "/project/Models/First.cs")
            .AddDocument(existingId, SecondFile, SourceText.From(ExistingText), ["Other"], "/project/Other/Second.cs");
        var document = solution.GetDocument(id)!;
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(document, type, SecondFile, CancellationToken.None);
        var added = changed.GetProject(project.Id)!.Documents.Single(item => item.Id != id && item.Id != existingId);
        await Assert.That(added.FilePath).IsEqualTo(Path.GetFullPath("/project/Models/Second.cs"));
        await Assert.That((await added.GetTextAsync()).ToString()).IsEqualTo("class Second {}\n");
        await Assert.That((await changed.GetDocument(existingId)!.GetTextAsync()).ToString()).IsEqualTo(ExistingText);
    }

    /// <summary>Checks a source-name collision is rejected before a lightbulb action is registered.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CollidingDestinationDoesNotRegisterAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(SecondFile, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.SingleType, type.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1402MoveTypeToFileCodeFixProvider>().CreateContainer();
        var actions = new List<CodeAction>();
        await container.GetExport<CodeFixProvider>().RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Checks an existing file excluded from the project is never overwritten.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PhysicalFileCollisionLeavesSolutionUnchangedAsync()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, SecondFile), ExistingText);
            using var workspace = new AdhocWorkspace();
            var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
            var id = DocumentId.CreateNewId(project.Id);
            var document = project.Solution.AddDocument(id, FirstFile, SourceText.From(Source), filePath: Path.Combine(directory.FullName, FirstFile)).GetDocument(id)!;
            var root = (await document.GetSyntaxRootAsync())!;
            var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
            var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(document, type, SecondFile, CancellationToken.None);
            await Assert.That(changed).IsSameReferenceAs(document.Project.Solution);
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(directory.FullName, SecondFile))).IsEqualTo(ExistingText);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>Checks registration and direct application decline hosts that unconditionally add Compile items.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MsBuildWorkspaceDoesNotOfferOrApplyExtractionAsync()
    {
        using var workspace = new AdhocWorkspace(MefHostServices.DefaultHost, workspaceKind: WorkspaceKind.MSBuild);
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FirstFile, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.SingleType, type.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1402MoveTypeToFileCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changed = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(document, type, SecondFile, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(document.Project.Solution);
    }

    /// <summary>Checks Fix All observes the same source-collision and host restrictions as individual fixes.</summary>
    /// <param name="workspaceKind">The host applying the fix.</param>
    /// <param name="sourceName">The original file name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(WorkspaceKind.Host, SecondFile)]
    [Arguments(WorkspaceKind.MSBuild, FirstFile)]
    public async Task FixAllLeavesUnsafeDocumentUnchangedAsync(string workspaceKind, string sourceName)
    {
        using var workspace = new AdhocWorkspace(MefHostServices.DefaultHost, workspaceKind);
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(sourceName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.SingleType, type.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1402MoveTypeToFileCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            nameof(Sst1402MoveTypeToFileCodeFixProvider),
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics(diagnostic),
            CancellationToken.None);
        var action = (await provider.GetFixAllProvider()!.GetFixAsync(context))!;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution ?? document.Project.Solution;
        await Assert.That(changed).IsSameReferenceAs(document.Project.Solution);
    }

    /// <summary>Supplies the diagnostic for the unsafe document Fix All request.</summary>
    /// <param name="diagnostic">The diagnostic returned to the provider.</param>
    private sealed class SelectedDiagnostics(Diagnostic diagnostic) : FixAllContext.DiagnosticProvider
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(ImmutableArray.Create(diagnostic));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(ImmutableArray.Create(diagnostic));
    }
}
