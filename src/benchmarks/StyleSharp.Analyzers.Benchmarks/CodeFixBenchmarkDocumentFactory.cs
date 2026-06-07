// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Creates Roslyn documents for code-fix benchmarks.</summary>
internal static class CodeFixBenchmarkDocumentFactory
{
    /// <summary>Creates one in-memory C# document with the benchmark reference set.</summary>
    /// <param name="workspace">The workspace that owns the document.</param>
    /// <param name="source">The source text to load.</param>
    /// <returns>The created document.</returns>
    public static Document CreateDocument(AdhocWorkspace workspace, string source)
    {
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                name: "Bench",
                assemblyName: "Bench",
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
                metadataReferences: BenchmarkCompilationFactory.MetadataReferences))
            .AddDocument(documentId, "Bench.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }
}
