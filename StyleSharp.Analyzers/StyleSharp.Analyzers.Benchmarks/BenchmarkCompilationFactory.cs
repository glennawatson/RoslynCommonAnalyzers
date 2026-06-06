// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Creates benchmark syntax trees and compilations against the host runtime reference set.</summary>
internal static class BenchmarkCompilationFactory
{
    private static readonly MetadataReference[] References = LoadReferences();
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    /// <summary>Parses C# source with the benchmark project's default parse options.</summary>
    /// <param name="source">The source text to parse.</param>
    /// <returns>The parsed syntax tree.</returns>
    public static SyntaxTree Parse(string source, string filePath = "Bench.cs") => CSharpSyntaxTree.ParseText(source, ParseOptions, filePath);

    /// <summary>Builds a library compilation from one syntax tree.</summary>
    /// <param name="source">The source text to compile.</param>
    /// <returns>The compiled syntax tree and compilation.</returns>
    public static (SyntaxTree Tree, CSharpCompilation Compilation) CreateCompilation(string source, string filePath = "Bench.cs")
    {
        var tree = Parse(source, filePath);
        return (
            tree,
            CSharpCompilation.Create(
                "Bench",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
    }

    private static MetadataReference[] LoadReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(static path => path.Length > 0)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
