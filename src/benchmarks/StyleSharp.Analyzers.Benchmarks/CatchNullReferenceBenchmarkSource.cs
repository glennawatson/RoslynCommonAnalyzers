// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for caught-null-reference analyzer benchmarks (SST2401).</summary>
internal static class CatchNullReferenceBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating catch clauses.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose handlers all name a failure they can handle.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Covers both rejection routes: a clause naming another type, and a filter that mentions one.</remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public void Run(string value)
               {
                   try
                   {
                       Console.WriteLine(value.Length);
                   }
                   catch (InvalidOperationException error)
                   {
                       Console.WriteLine(error.Message);
                   }
                   catch (Exception error) when (error is FormatException)
                   {
                       Console.WriteLine(error.Message);
                   }
               }
           }
           """;

    /// <summary>Builds one type that catches a null dereference instead of preventing it.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Run(string value)
               {
                   try
                   {
                       Console.WriteLine(value.Length);
                   }
                   catch (NullReferenceException error)
                   {
                       Console.WriteLine(error.Message);
                   }
               }
           }
           """;
}
