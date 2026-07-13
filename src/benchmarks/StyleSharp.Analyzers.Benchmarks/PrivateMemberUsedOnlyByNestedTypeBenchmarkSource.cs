// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for nested-type-only member analyzer benchmarks (SST1498).</summary>
internal static class PrivateMemberUsedOnlyByNestedTypeBenchmarkSource
{
    /// <summary>The number of members each violating type has handed to its nested type.</summary>
    public const int ViolationsPerType = 3;

    /// <summary>Builds a compilation unit that exercises clean or violating member placement.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether the nested type should be the only user of the members.</param>
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

    /// <summary>Builds one type that nests a type but keeps its private members for itself.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The type nests a type, so it walks the whole analysis rather than stopping at the syntactic gate: this
    /// is the clean path that costs something. It covers each way a member escapes the rule — one the type
    /// itself also uses, one two nested types share, and one nothing uses at all.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private static int Shared(int value) => value * 2;

               private static int Owned(int value) => value + 1;

               private static int _factor = 3;

               public int Run(int value) => Owned(value) * _factor;

               public sealed class First
               {
                   public int Run(int value) => Shared(value) + Owned(value);
               }

               public sealed class Second
               {
                   public int Run(int value) => Shared(value);
               }
           }
           """;

    /// <summary>Builds one type whose private members only its nested type uses.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Each type hands exactly <see cref="ViolationsPerType"/> members — a method, a field and a property — to its nested type.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private static int Double(int value) => value * 2;

               private static int _factor = 3;

               private static int Offset => 7;

               public int Run(int value) => value;

               public sealed class Inner
               {
                   public int Run(int value) => (Double(value) * _factor) + Offset;
               }
           }
           """;
}
