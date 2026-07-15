// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Builds a compiled syntax tree and its semantic model for helper-level tests.</summary>
internal static class SemanticModelFactory
{
    /// <summary>Compiles a single source snippet and returns its root and semantic model.</summary>
    /// <param name="source">The source to compile.</param>
    /// <returns>The compilation unit root and the semantic model over it.</returns>
    public static (CompilationUnitSyntax Root, SemanticModel Model) Create(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName: "SemanticModelFactory",
            syntaxTrees: [tree],
            references: CreateReferences());
        return (tree.GetCompilationUnitRoot(), compilation.GetSemanticModel(tree));
    }

    /// <summary>Creates metadata references for the current runtime.</summary>
    /// <returns>The metadata references.</returns>
    private static MetadataReference[] CreateReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = new MetadataReference[trustedAssemblies.Length];
        for (var i = 0; i < trustedAssemblies.Length; i++)
        {
            references[i] = MetadataReference.CreateFromFile(trustedAssemblies[i]);
        }

        return references;
    }
}
