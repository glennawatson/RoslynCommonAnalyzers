// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for empty-comment analyzer benchmarks (SST1659).</summary>
internal static class EmptyCommentBenchmarkSource
{
    /// <summary>The number of empty comments each violating type declares.</summary>
    public const int ViolationsPerType = 4;

    /// <summary>Builds a compilation unit that exercises clean or violating comment shapes.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit empty comments.</param>
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

    /// <summary>Builds one type whose comments all say something.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection the clean path makes: a comment with text, a block comment with text, the
    /// commented-out code marker, and a documentation comment whose blank line is deliberate formatting.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           /// <summary>A counter.</summary>
           ///
           /// <remarks>The blank line above is formatting, not an empty comment.</remarks>
           public sealed class C{{index}}
           {
               // The running total.
               private int _total; /* counted on every add */

               //// private int _disabled;

               /// <summary>Adds one.</summary>
               /// <returns>The new total.</returns>
               public int Increment() => ++_total;
           }
           """;

    /// <summary>Builds one type whose comments are all empty.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Each type carries exactly <see cref="ViolationsPerType"/> empty comments.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           ///
           public sealed class V{{index}}
           {
               //
               private int _total;

               /* */
               public int Increment() //
               {
                   return ++_total;
               }
           }
           """;
}
