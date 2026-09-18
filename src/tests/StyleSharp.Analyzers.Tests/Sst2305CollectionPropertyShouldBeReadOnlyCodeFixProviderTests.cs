// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyCollectionProperty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer,
    StyleSharp.Analyzers.Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests setter removal when collection properties have existing writers.</summary>
public sealed class Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProviderTests
{
    /// <summary>The source document name used by single-document tests.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies replacing a resized array prevents setter removal, including Fix All.</summary>
    /// <param name="fixAll">Whether to invoke the batch fix.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ResizedArrayAssignmentHasNoFixAsync(bool fixAll, CancellationToken cancellationToken)
    {
        const string Source = """
                              using System;

                              internal static class Example
                              {
                                  public static int Grow()
                                  {
                                      var bucket = new Bucket(1);
                                      var items = bucket.Items;
                                      Array.Resize(ref items, 2);
                                      bucket.Items = items;
                                      return bucket.Items.Length;
                                  }

                                  internal sealed class Bucket(int capacity)
                                  {
                                      public int[] Items { get; set; } = new int[capacity];
                                  }
                              }
                              """;
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var document = project.AddDocument(DocumentName, Source);
        await VerifyNoFixAsync(document, fixAll, cancellationToken);
    }

    /// <summary>Verifies every assignment form retains the setter it invokes.</summary>
    /// <param name="statement">The statement that writes to the collection property.</param>
    /// <param name="fixAll">Whether to invoke the batch fix.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task AssignmentFormsHaveNoFixAsync(
        [Matrix(
            "Items = new int[0];",
            "(Items) = new int[0];",
            "Items ??= new int[0];",
            "_ = new C { Items = new int[0] };",
            "(Items, _) = (new int[0], 1);",
            "var other = new C(); other?.Items = new int[0];",
            "System.Action set = () => Items = new int[0]; set();")] string statement,
        [Matrix(false, true)] bool fixAll,
        CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var document = project.AddDocument(DocumentName, $$"""
            class C
            {
                public int[] Items { get; set; }
                void M() { {{statement}} }
            }
            """);
        await VerifyNoFixAsync(document, fixAll, cancellationToken);
    }

    /// <summary>Verifies references to constructed generic properties are checked across the solution.</summary>
    /// <param name="otherProject">Whether the writer lives in a referencing project.</param>
    /// <param name="fixAll">Whether to invoke the batch fix.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task WriterInAnotherDocumentHasNoFixAsync(
        [Matrix(false, true)] bool otherProject,
        [Matrix(false, true)] bool fixAll,
        CancellationToken cancellationToken)
    {
        const string Source = "public class Bucket<T> { public T[] Items { get; set; } }";
        const string Writer = "class Writer { void M(Bucket<int> bucket) { bucket.Items = new int[0]; } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Library", LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var document = project.AddDocument("Bucket.cs", Source);
        var writerProject = otherProject
            ? document.Project.Solution.AddProject("Consumer", "Consumer", LanguageNames.CSharp)
                .WithMetadataReferences(RuntimeMetadataReferences.Platform)
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddProjectReference(new(project.Id))
            : document.Project;
        var writer = writerProject.AddDocument("Writer.cs", Writer);
        var compilation = (await writer.Project.GetCompilationAsync(cancellationToken))!;
        await Assert.That(compilation.GetDiagnostics(cancellationToken).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await VerifyNoFixAsync(writer.Project.Solution.GetDocument(document.Id)!, fixAll, cancellationToken);
    }

    /// <summary>Verifies overloaded increment and decrement require the property's setter.</summary>
    /// <param name="statement">The increment or decrement expression.</param>
    /// <param name="fixAll">Whether to invoke the batch fix.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task IncrementAndDecrementHaveNoFixAsync(
        [Matrix("Items++;", "++Items;", "Items--;", "--Items;")] string statement,
        [Matrix(false, true)] bool fixAll,
        CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var document = project.AddDocument(DocumentName, $$"""
            class Bag : System.Collections.Generic.List<int>
            {
                public static Bag operator ++(Bag value) => new Bag();
                public static Bag operator --(Bag value) => new Bag();
            }
            class C
            {
                public Bag Items { get; set; } = new Bag();
                void M() { {{statement}} }
            }
            """);
        await VerifyNoFixAsync(document, fixAll, cancellationToken);
    }

    /// <summary>Verifies a named attribute argument retains the setter used to initialize its array.</summary>
    /// <param name="fixAll">Whether to invoke the batch fix.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AttributeArgumentHasNoFixAsync(bool fixAll, CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var document = project.AddDocument(DocumentName, """
            class AAttribute : System.Attribute { public int[] Items { get; set; } }
            [A(Items = new int[] { 1 })] class C { }
            """);
        await VerifyNoFixAsync(document, fixAll, cancellationToken);
    }

    /// <summary>Verifies reads, content mutations and writes to unrelated members allow setter removal.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContentMutationsAllowSetterRemovalAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              class C
                              {
                                  public List<int> {|SST2305:Items|} { get; set; } = new List<int> { 0 };
                                  void M(Other other)
                                  {
                                      Items[0] = 1;
                                      Items.Add(2);
                                      other.Items = Items;
                                      _ = new C { Items = { 3 } };
                                  }
                              }
                              class Other { public List<int> Items; }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   class C
                                   {
                                       public List<int> Items { get; } = new List<int> { 0 };
                                       void M(Other other)
                                       {
                                           Items[0] = 1;
                                           Items.Add(2);
                                           other.Items = Items;
                                           _ = new C { Items = { 3 } };
                                       }
                                   }
                                   class Other { public List<int> Items; }
                                   """;
        await VerifyCollectionProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Checks the diagnostic remains while both fix paths preserve the source.</summary>
    /// <param name="document">The property document in its complete solution.</param>
    /// <param name="fixAll">Whether to invoke the batch fix.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifyNoFixAsync(Document document, bool fixAll, CancellationToken cancellationToken)
    {
        var compilation = (await document.Project.GetCompilationAsync(cancellationToken))!;
        await Assert.That(compilation.GetDiagnostics(cancellationToken).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var diagnostics = await compilation.WithAnalyzers([new Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2305");
        using var container = new ContainerConfiguration().WithPart<Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        if (!fixAll)
        {
            var actions = new List<CodeAction>();
            await provider.RegisterCodeFixesAsync(new(document, diagnostics[0], (action, _) => actions.Add(action), cancellationToken));
            await Assert.That(actions).IsEmpty();
            return;
        }

        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            nameof(Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProvider),
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics(diagnostics),
            cancellationToken);
        var action = await provider.GetFixAllProvider()!.GetFixAsync(context);
        var operations = await action!.GetOperationsAsync(cancellationToken);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync(cancellationToken)).ToString()).IsEqualTo((await document.GetTextAsync(cancellationToken)).ToString());
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
