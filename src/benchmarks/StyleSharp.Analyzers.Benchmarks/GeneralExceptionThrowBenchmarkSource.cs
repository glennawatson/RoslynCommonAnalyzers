// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for general-exception-throw analyzer benchmarks (SST2409).</summary>
internal static class GeneralExceptionThrowBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating throws.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose throws all name the failure.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Covers both throw forms — the statement and the expression — with a specific type in each.</remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string Get(string value)
               {
                   if (value.Length == 0)
                   {
                       throw new ArgumentException("empty", nameof(value));
                   }

                   return value ?? throw new InvalidOperationException("missing");
               }
           }
           """;

    /// <summary>Builds one type that throws a general and a runtime-reserved exception type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public string Get(string value)
               {
                   if (value.Length == 0)
                   {
                       throw new Exception("empty");
                   }

                   return value ?? throw new NullReferenceException();
               }
           }
           """;
}
