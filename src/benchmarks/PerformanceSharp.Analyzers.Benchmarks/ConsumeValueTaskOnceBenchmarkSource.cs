// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for consume-value-task-once analyzer benchmarks.</summary>
internal static class ConsumeValueTaskOnceBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating value-task consumption.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit consume-value-task-once rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating value-task-consumption type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type that creates a fresh value task on each loop iteration.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private static System.Threading.Tasks.ValueTask P() => default;

               public async System.Threading.Tasks.Task M()
               {
                   for (int j = 0; j < 3; j++)
                   {
                       System.Threading.Tasks.ValueTask vt = P();
                       await vt;
                   }
               }
           }
           """;

    /// <summary>Builds one violating type that awaits a single value task across loop iterations.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private static System.Threading.Tasks.ValueTask P() => default;

               public async System.Threading.Tasks.Task M()
               {
                   System.Threading.Tasks.ValueTask vt = P();
                   for (int j = 0; j < 3; j++)
                   {
                       await vt;
                   }
               }
           }
           """;
}
