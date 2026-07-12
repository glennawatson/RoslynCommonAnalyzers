// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for identical-branch analyzer benchmarks.</summary>
internal static class IdenticalBranchesBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating identical-branch patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit identical-branch rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating identical-branch type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose branches all decide something, producing no diagnostics.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: an if/else whose bodies differ, an if with no
    /// else, a switch whose duplicated sections have no default to make them exhaustive, a switch expression
    /// whose arms differ, and a conditional expression whose arms differ. Exactly zero diagnostics — and no
    /// construct reaches the semantic model, which is what this corpus exists to prove.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Classify(int value)
               {
                   if (value < 0)
                   {
                       return -1;
                   }
                   else
                   {
                       return 1;
                   }
               }

               public int Partial(int value)
               {
                   if (value < 0)
                   {
                       return -1;
                   }

                   return 0;
               }

               public int Route(int value)
               {
                   switch (value)
                   {
                       case 1:
                           return 10;
                       case 2:
                           return 10;
                   }

                   return 0;
               }

               public string Name(int value) => value switch
               {
                   1 => "one",
                   _ => "other",
               };

               public int Pick(bool flag) => flag ? 1 : 2;
           }
           """;

    /// <summary>Builds one type whose branches all do the same thing.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Five diagnostics per block: the if/else, the three-branch chain, the switch statement, the switch
    /// expression, and the conditional expression.
    /// </remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Classify(int value)
               {
                   if (value < 0)
                   {
                       return 1;
                   }
                   else
                   {
                       return 1;
                   }
               }

               public int Chain(int value)
               {
                   if (value < 0)
                   {
                       return 2;
                   }
                   else if (value > 10)
                   {
                       return 2;
                   }
                   else
                   {
                       return 2;
                   }
               }

               public int Route(int value)
               {
                   switch (value)
                   {
                       case 1:
                           return 3;
                       default:
                           return 3;
                   }
               }

               public string Name(int value) => value switch
               {
                   1 => "same",
                   _ => "same",
               };

               public int Pick(bool flag) => flag ? 4 : 4;
           }
           """;
}
