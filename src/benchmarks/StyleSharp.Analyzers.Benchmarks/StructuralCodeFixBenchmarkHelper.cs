// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds reusable benchmark state for the structural/layout code-fix benchmark family.</summary>
internal static class StructuralCodeFixBenchmarkHelper
{
    /// <summary>Divides the synthetic node count to select a representative midpoint node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>Creates one benchmark context for the requested source pattern and target syntax node type.</summary>
    /// <typeparam name="TNode">The syntax node type the benchmark will pass to the provider helper.</typeparam>
    /// <param name="nodes">The synthetic node count.</param>
    /// <param name="sourceFactory">Builds the synthetic source text.</param>
    /// <param name="nodeSelector">Selects the representative target node from the parsed root.</param>
    /// <returns>The prepared benchmark context.</returns>
    public static async Task<StructuralCodeFixBenchmarkContext<TNode>> CreateAsync<TNode>(
        int nodes,
        Func<int, string> sourceFactory,
        Func<CompilationUnitSyntax, int, TNode> nodeSelector)
        where TNode : SyntaxNode
    {
        var workspace = new AdhocWorkspace();
        var document = CodeFixBenchmarkDocumentFactory.CreateDocument(workspace, sourceFactory(nodes));
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        return new StructuralCodeFixBenchmarkContext<TNode>(workspace, document, root, nodeSelector(root, nodes / MiddleNodeDivisor));
    }
}
