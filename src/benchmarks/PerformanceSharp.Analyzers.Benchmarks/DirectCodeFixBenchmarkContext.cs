// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Holds the prepared Roslyn objects for one direct code-fix benchmark instance.</summary>
/// <typeparam name="TTarget">The target shape passed to the provider helper.</typeparam>
internal sealed class DirectCodeFixBenchmarkContext<TTarget> : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="DirectCodeFixBenchmarkContext{TTarget}"/> class.</summary>
    /// <param name="workspace">The workspace owning the benchmark document.</param>
    /// <param name="document">The benchmark document.</param>
    /// <param name="root">The cached syntax root.</param>
    /// <param name="target">The representative target.</param>
    public DirectCodeFixBenchmarkContext(AdhocWorkspace workspace, Document document, CompilationUnitSyntax root, TTarget target)
    {
        Workspace = workspace;
        Document = document;
        Root = root;
        Target = target;
    }

    /// <summary>Gets the workspace owning the benchmark document.</summary>
    public AdhocWorkspace Workspace { get; }

    /// <summary>Gets the benchmark document.</summary>
    public Document Document { get; }

    /// <summary>Gets the cached syntax root.</summary>
    public CompilationUnitSyntax Root { get; }

    /// <summary>Gets the representative target passed to the provider helper.</summary>
    public TTarget Target { get; }

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    public void Dispose() => Workspace.Dispose();
}
