// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if ROSLYN_5_9_OR_GREATER

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the PSH1025 boxing-union-case analyzer benchmarks.</summary>
internal static class Psh1025BoxingUnionCaseBenchmarkSource
{
    /// <summary>The runtime support a union declaration binds against, declared once per compilation.</summary>
    private const string UnionSupport = """
                                        namespace System.Runtime.CompilerServices
                                        {
                                            public interface IUnion { }

                                            [System.AttributeUsage(System.AttributeTargets.All)]
                                            public sealed class UnionAttribute : System.Attribute { }
                                        }
                                        """;

    /// <summary>Builds a compilation unit of clean or violating union declarations.</summary>
    /// <param name="types">The number of synthetic unions to emit.</param>
    /// <param name="violating">Whether to emit boxing-union-case rule violations.</param>
    /// <returns>The generated source text.</returns>
    internal static string Generate(int types, bool violating) =>
        $$"""
           #nullable enable
           {{UnionSupport}}

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateUnion(i, violating))}}
           """;

    /// <summary>Builds one clean or violating union.</summary>
    /// <param name="index">The synthetic union index.</param>
    /// <param name="violating">Whether to emit a violating union.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateUnion(int index, bool violating) => violating
        ? $"public union Reading{index}(int, double, string);"
        : $"public union Payload{index}(string, byte[], System.Uri);";
}

#endif
