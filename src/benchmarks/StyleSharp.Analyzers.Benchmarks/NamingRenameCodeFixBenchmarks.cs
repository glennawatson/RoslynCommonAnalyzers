// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the shared naming-rename code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class NamingRenameCodeFixBenchmarks
{
    /// <summary>Identifies the representative field member within each generated type.</summary>
    private const int RepresentativeFieldMemberIndex = 4;

    /// <summary>Stores the prepared benchmark document and representative variable declarator.</summary>
    private StructuralCodeFixBenchmarkContext<VariableDeclaratorSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative private field declaration.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            static count => NamingBenchmarkSource.GenerateFieldSource(count, violating: true),
            static (root, index)
                => ((FieldDeclarationSyntax)CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(root, index).Members[RepresentativeFieldMemberIndex])
                    .Declaration
                    .Variables[0]).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the shared rename fix to one representative field-name violation.</summary>
    /// <returns>The number of projects in the updated solution.</returns>
    [Benchmark]
    public async Task<int> NamingRename_ApplyFixAsync()
    {
        var updated = await NamingRenameCodeFixProvider.RenameAsync(
            _context.Document,
            _context.Node,
            BuildNewName(_context.Node.Identifier.ValueText),
            CancellationToken.None).ConfigureAwait(false);
        return updated.ProjectIds.Count;
    }

    /// <summary>Builds the replacement field name expected by the naming rename benchmark.</summary>
    /// <param name="name">The original field name.</param>
    /// <returns>The renamed field identifier.</returns>
    private static string BuildNewName(string name)
        => name.Length == 0 ? "_value" : $"_{char.ToLowerInvariant(name[0])}{name.Substring(1)}";
}
