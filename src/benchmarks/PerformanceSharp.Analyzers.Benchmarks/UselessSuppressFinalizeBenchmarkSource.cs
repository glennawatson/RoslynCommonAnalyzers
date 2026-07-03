// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for useless-SuppressFinalize analyzer benchmarks.</summary>
internal static class UselessSuppressFinalizeBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating useless-SuppressFinalize patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit useless-SuppressFinalize rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating useless-SuppressFinalize type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one sealed type whose finalizer makes the call meaningful.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}} : System.IDisposable
           {
               ~C{{index}}()
               {
               }
           
               public void Dispose()
               {
                   System.GC.SuppressFinalize(this);
               }
           }
           """;

    /// <summary>Builds one sealed finalizer-free type whose call does nothing.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}} : System.IDisposable
           {
               public void Dispose()
               {
                   System.GC.SuppressFinalize(this);
               }
           }
           """;
}
