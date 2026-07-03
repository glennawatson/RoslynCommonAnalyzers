// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for use-environment-properties analyzer benchmarks.</summary>
internal static class UseEnvironmentPropertiesBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating current-process/thread access patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit use-environment-properties rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating current-process/thread access type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type that already reads the current-process/thread state via Environment properties.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string M()
               {
                   var id = System.Environment.ProcessId;
                   var threadId = System.Environment.CurrentManagedThreadId;
                   var path = System.Environment.ProcessPath;
                   return path + id + threadId;
               }
           }
           """;

    /// <summary>Builds one violating type that reaches current-process/thread state the expensive way.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string M()
               {
                   var id = System.Diagnostics.Process.GetCurrentProcess().Id;
                   var path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                   var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                   return path + id + threadId;
               }
           }
           """;
}
