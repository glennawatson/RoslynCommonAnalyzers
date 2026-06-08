// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds reusable benchmark state for the unique-lines code-fix family.</summary>
internal static class UniqueLinesCodeFixBenchmarkHelper
{
    /// <summary>Creates one benchmark context for the requested source pattern and target syntax node type.</summary>
    /// <typeparam name="TNode">The syntax node type the benchmark will pass to the provider helper.</typeparam>
    /// <param name="members">The synthetic member count.</param>
    /// <param name="sourceFactory">Builds the synthetic source text.</param>
    /// <param name="allowUnsafe">Whether the benchmark document should allow unsafe code.</param>
    /// <returns>The prepared benchmark context.</returns>
    public static async Task<UniqueLinesCodeFixBenchmarkContext<TNode>> CreateAsync<TNode>(
        int members,
        Func<int, string> sourceFactory,
        bool allowUnsafe = false)
        where TNode : SyntaxNode
    {
        var workspace = new AdhocWorkspace();
        var document = CodeFixBenchmarkDocumentFactory.CreateDocument(workspace, sourceFactory(members));
        if (allowUnsafe)
        {
            var compilationOptions = (CSharpCompilationOptions)document.Project.CompilationOptions!;
            var solution = document.Project.Solution.WithProjectCompilationOptions(
                document.Project.Id,
                compilationOptions.WithAllowUnsafe(true));
            document = solution.GetDocument(document.Id)!;
        }

        var root = (await document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        return new UniqueLinesCodeFixBenchmarkContext<TNode>(workspace, document, root, FindMiddleNode<TNode>(root));
    }

    /// <summary>Finds the middle node of the requested type so each benchmark applies the fix to a representative violation.</summary>
    /// <typeparam name="TNode">The syntax node type to locate.</typeparam>
    /// <param name="root">The root node to search.</param>
    /// <returns>The selected syntax node.</returns>
    private static TNode FindMiddleNode<TNode>(SyntaxNode root)
        where TNode : SyntaxNode
    {
        var nodeCount = 0;
        foreach (var candidate in root.DescendantNodes())
        {
            if (candidate is TNode)
            {
                nodeCount++;
            }
        }

        if (nodeCount == 0)
        {
            throw new InvalidOperationException($"No {typeof(TNode).Name} nodes were found in the benchmark source.");
        }

        var targetIndex = nodeCount / 2;
        var currentIndex = 0;
        foreach (var candidate in root.DescendantNodes())
        {
            if (candidate is not TNode node)
            {
                continue;
            }

            if (currentIndex == targetIndex)
            {
                return node;
            }

            currentIndex++;
        }

        throw new InvalidOperationException($"Unable to select the middle {typeof(TNode).Name} node from the benchmark source.");
    }
}
