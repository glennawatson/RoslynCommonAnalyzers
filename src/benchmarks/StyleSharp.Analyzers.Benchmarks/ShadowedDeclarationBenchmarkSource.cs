// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for shadowed-declaration analyzer benchmarks.</summary>
internal static class ShadowedDeclarationBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or shadowing declarations.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit shadowed-declaration rule violations.</param>
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

    /// <summary>Builds one type whose declarations are all clean.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: names that miss the member table entirely
    /// (the common case), a constructor parameter that feeds the field it shadows, and a local in a static
    /// method that reuses an instance field's name, which is not in scope there.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly string name;

               private int count;

               public C{{index}}(string name) => this.name = name;

               public static int Measure(string text)
               {
                   var count = text.Length;
                   return count;
               }

               public int Add(int[] values)
               {
                   var total = 0;
                   foreach (var value in values)
                   {
                       total += value;
                   }

                   count += total;
                   return count;
               }

               public string Describe() => name;
           }
           """;

    /// <summary>Builds one type whose declarations all shadow a member.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Emits four diagnostics: a parameter, a local, a loop variable and an out variable.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private int count;

               private string name;

               public int Load(string name) => name.Length;

               public int Total(int[] values)
               {
                   var count = 0;
                   foreach (var name in values)
                   {
                       count += name;
                   }

                   return count;
               }

               public bool Parse(string text)
               {
                   if (int.TryParse(text, out var count))
                   {
                       return count > 0;
                   }

                   return false;
               }

               public string Describe() => name + count;
           }
           """;
}
