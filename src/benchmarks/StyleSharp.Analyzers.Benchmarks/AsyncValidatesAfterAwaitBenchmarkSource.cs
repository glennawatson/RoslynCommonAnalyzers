// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the async-validates-after-await analyzer benchmarks.</summary>
internal static class AsyncValidatesAfterAwaitBenchmarkSource
{
    /// <summary>Builds a compilation unit whose async methods validate before or after their first await.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to validate after the first await.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating async-method type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one async method that validates before its first await.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public async System.Threading.Tasks.Task M(string value)
               {
                   if (value is null)
                   {
                       throw new System.ArgumentNullException(nameof(value));
                   }

                   await System.Threading.Tasks.Task.Yield();
               }
           }
           """;

    /// <summary>Builds one async method that validates after its first await.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public async System.Threading.Tasks.Task M(string value)
               {
                   await System.Threading.Tasks.Task.Yield();
                   if (value is null)
                   {
                       throw new System.ArgumentNullException(nameof(value));
                   }
               }
           }
           """;
}
