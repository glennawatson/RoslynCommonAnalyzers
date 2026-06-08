// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds reusable benchmark state for direct code-fix helper benchmarks.</summary>
internal static class DirectCodeFixBenchmarkHelper
{
    /// <summary>Creates one benchmark context for the requested source pattern and target shape.</summary>
    /// <typeparam name="TTarget">The target shape passed to the provider helper.</typeparam>
    /// <param name="count">The synthetic node or type count.</param>
    /// <param name="sourceFactory">Builds the synthetic source text.</param>
    /// <param name="targetFactory">Selects the representative benchmark target from the parsed root.</param>
    /// <returns>The prepared benchmark context.</returns>
    public static async Task<DirectCodeFixBenchmarkContext<TTarget>> CreateAsync<TTarget>(
        int count,
        Func<int, string> sourceFactory,
        Func<Document, CompilationUnitSyntax, int, Task<TTarget>> targetFactory)
    {
        var workspace = new AdhocWorkspace();
        var document = CodeFixBenchmarkDocumentFactory.CreateDocument(workspace, sourceFactory(count));
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        var target = await targetFactory(document, root, count / 2).ConfigureAwait(false);
        return new DirectCodeFixBenchmarkContext<TTarget>(workspace, document, root, target);
    }
}
