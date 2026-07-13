// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for overload-grouping analyzer benchmarks (SST1218).</summary>
internal static class OverloadsGroupedBenchmarkSource
{
    /// <summary>The number of out-of-place overloads each violating type declares.</summary>
    public const int ViolationsPerType = 2;

    /// <summary>Builds a compilation unit that exercises clean or violating member orders.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to scatter the overloads.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose overloads all sit together.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers the rejections the clean path makes: adjacent overloads, a comment between two of them, and an
    /// overload the ordering rules place elsewhere — a private one, and a static one — which is not compared.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public static string Format(int value) => value.ToString();

               public void Write(int value)
               {
               }

               // Text goes through the same path.
               public void Write(string value)
               {
               }

               public void Flush()
               {
               }

               private void Write(bool value)
               {
               }
           }
           """;

    /// <summary>Builds one type whose overloads are scattered.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Each type carries exactly <see cref="ViolationsPerType"/> out-of-place overloads.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Write(int value)
               {
               }

               public void Flush()
               {
               }

               public void Write(string value)
               {
               }

               public void Close()
               {
               }

               public void Write(bool value)
               {
               }
           }
           """;
}
