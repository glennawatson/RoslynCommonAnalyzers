// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1315 union-member naming analyzer benchmarks.</summary>
internal static class Sst1315UnionMemberNamingBenchmarkSource
{
    /// <summary>
    /// Builds a compilation unit of clean or violating union cases. The rule is gated on a
    /// structurally detected <c>System.Runtime.CompilerServices.IUnion</c> marker, so the
    /// marker interface and a union base type implementing it are emitted once; each case
    /// derives from that base so the analyzer treats it as union-related. The types are
    /// emitted at the top level (no enclosing namespace) so the marker can sit in its own
    /// block namespace, mirroring the SST1315 unit test.
    /// </summary>
    /// <param name="types">The number of synthetic union cases to emit.</param>
    /// <param name="violating">Whether to emit union-member naming rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace System.Runtime.CompilerServices
           {
               public interface IUnion
               {
               }
           }

           public abstract class Shape : System.Runtime.CompilerServices.IUnion
           {
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating union case.</summary>
    /// <param name="index">The synthetic case index.</param>
    /// <param name="violating">Whether to emit a violating case.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean union case whose name is PascalCase.</summary>
    /// <param name="index">The synthetic case index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class Circle{{index}} : Shape
           {
           }
           """;

    /// <summary>Builds one violating union case whose name is not PascalCase.</summary>
    /// <param name="index">The synthetic case index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class circle{{index}} : Shape
           {
           }
           """;
}
