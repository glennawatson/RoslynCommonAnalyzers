// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Creates benchmark syntax trees and compilations against the host runtime reference set.</summary>
internal static class BenchmarkCompilationFactory
{
    /// <summary>The metadata references loaded from the current host runtime.</summary>
    private static readonly MetadataReference[] References = LoadReferences();

    /// <summary>The parse options used for all benchmark syntax trees.</summary>
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    /// <summary>Gets the metadata references loaded from the current host runtime.</summary>
    public static IReadOnlyList<MetadataReference> MetadataReferences => References;

    /// <summary>Parses C# source with the benchmark project's default parse options.</summary>
    /// <param name="source">The source text to parse.</param>
    /// <param name="filePath">The file path to use for the syntax tree.</param>
    /// <returns>The parsed syntax tree.</returns>
    public static SyntaxTree Parse(string source, string filePath = "Bench.cs") => CSharpSyntaxTree.ParseText(source, ParseOptions, filePath);

    /// <summary>Builds a library compilation from one syntax tree.</summary>
    /// <param name="source">The source text to compile.</param>
    /// <param name="filePath">The file path to use for the syntax tree.</param>
    /// <returns>The compiled syntax tree and compilation.</returns>
    public static (SyntaxTree Tree, CSharpCompilation Compilation) CreateCompilation(string source, string filePath = "Bench.cs")
        => CreateCompilation(source, [], filePath);

    /// <summary>Builds a library compilation from one syntax tree, enabling the supplied rule ids.</summary>
    /// <param name="source">The source text to compile.</param>
    /// <param name="enabledRuleIds">Diagnostic ids to force on (so opt-in analyzers actually run and report).</param>
    /// <param name="filePath">The file path to use for the syntax tree.</param>
    /// <returns>The compiled syntax tree and compilation.</returns>
    public static (SyntaxTree Tree, CSharpCompilation Compilation) CreateCompilation(
        string source,
        IReadOnlyList<string> enabledRuleIds,
        string filePath = "Bench.cs")
    {
        var tree = Parse(source, filePath);
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        if (enabledRuleIds.Count > 0)
        {
            var overrides = ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();
            for (var i = 0; i < enabledRuleIds.Count; i++)
            {
                overrides[enabledRuleIds[i]] = ReportDiagnostic.Warn;
            }

            options = options.WithSpecificDiagnosticOptions(overrides.ToImmutable());
        }

        return (tree, CSharpCompilation.Create("Bench", [tree], References, options));
    }

    /// <summary>Loads metadata references for the current host runtime.</summary>
    /// <returns>The runtime metadata references.</returns>
    private static MetadataReference[] LoadReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return
        [
            ..
            trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(static path => path.Length > 0)
                .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }
}
