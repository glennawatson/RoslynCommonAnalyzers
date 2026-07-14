// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for global-namespace analysis (SST2312).</summary>
internal static class GlobalNamespaceBenchmarkSource
{
    /// <summary>Builds a compilation unit whose types either have a namespace or have none.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    /// <remarks>
    /// The two corpora differ in the one thing the rule looks at, so the clean one keeps its namespace and
    /// the violating one has none at all. The clean path is then exactly what a real file costs: a parent
    /// check on each type declaration, rejected without ever reaching the semantic model.
    /// </remarks>
    public static string Generate(int types, bool violating)
        => violating
            ? BenchmarkSourceText.JoinBlocks(types, GenerateViolatingType)
            : $$"""
              namespace Bench;

              {{BenchmarkSourceText.JoinBlocks(types, GenerateCleanType)}}
              """;

    /// <summary>Builds one type that already lives in a namespace.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public class C{{index}}
           {
               public int Size;

               public class Nested{{index}}
               {
                   public int Depth;
               }
           }

           public interface IC{{index}}
           {
               int Size { get; }
           }

           public enum Level{{index}}
           {
               Low,
               High,
           }
           """;

    /// <summary>Builds one type declared outside any namespace.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class V{{index}}
           {
               public int Size;

               public class Nested{{index}}
               {
                   public int Depth;
               }
           }

           public interface IV{{index}}
           {
               int Size { get; }
           }

           public enum Grade{{index}}
           {
               Low,
               High,
           }
           """;
}
