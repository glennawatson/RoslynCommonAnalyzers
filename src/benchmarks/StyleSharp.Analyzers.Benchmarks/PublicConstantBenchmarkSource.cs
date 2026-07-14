// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for visible-constant analysis (SST2311).</summary>
internal static class PublicConstantBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises reachable or hidden constants.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
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

    /// <summary>Builds one type whose constants no other assembly can bake in.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers the two rejection routes the clean path takes: the token test that rejects an ordinary field
    /// without binding anything — which is most fields in a real file — and, for the constants that do get
    /// bound, an accessibility or a containing type that keeps them inside the assembly.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public class C{{index}}
           {
               public static readonly int Maximum = {{index}};

               public int Size;

               internal const int Internal = 1;

               private const int Private = 2;

               private protected const int PrivateProtected = 3;

               public int Read() => Internal + Private + PrivateProtected + Maximum + Size;
           }

           internal class Hidden{{index}}
           {
               public const int StillHidden = {{index}};
           }
           """;

    /// <summary>Builds one type whose constants an outside caller copies in at its own compile time.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class V{{index}}
           {
               public const int Maximum = {{index}};

               public const string Name = "v{{index}}";

               protected const int Minimum = 1;

               protected internal const int Step = 2;
           }
           """;
}
