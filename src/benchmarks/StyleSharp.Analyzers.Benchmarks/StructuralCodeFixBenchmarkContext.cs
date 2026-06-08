// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Holds the prepared Roslyn objects for one structural/layout code-fix benchmark instance.</summary>
/// <typeparam name="TNode">The syntax node type passed to the provider helper.</typeparam>
internal sealed class StructuralCodeFixBenchmarkContext<TNode> : IDisposable
    where TNode : SyntaxNode
{
    /// <summary>Initializes a new instance of the <see cref="StructuralCodeFixBenchmarkContext{TNode}"/> class.</summary>
    /// <param name="workspace">The workspace owning the benchmark document.</param>
    /// <param name="document">The benchmark document.</param>
    /// <param name="root">The cached syntax root.</param>
    /// <param name="node">The representative target node.</param>
    public StructuralCodeFixBenchmarkContext(AdhocWorkspace workspace, Document document, CompilationUnitSyntax root, TNode node)
    {
        Workspace = workspace;
        Document = document;
        Root = root;
        Node = node;
    }

    /// <summary>Gets the workspace owning the benchmark document.</summary>
    public AdhocWorkspace Workspace { get; }

    /// <summary>Gets the benchmark document.</summary>
    public Document Document { get; }

    /// <summary>Gets the cached syntax root.</summary>
    public CompilationUnitSyntax Root { get; }

    /// <summary>Gets the representative target node.</summary>
    public TNode Node { get; }

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    public void Dispose() => Workspace.Dispose();
}
