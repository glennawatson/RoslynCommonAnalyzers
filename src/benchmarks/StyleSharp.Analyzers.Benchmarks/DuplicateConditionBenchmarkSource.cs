// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for duplicate-condition analyzer benchmarks.</summary>
internal static class DuplicateConditionBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating duplicate-condition patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit duplicate-condition rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating duplicate-condition type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose conditions never repeat, producing no diagnostics.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a long chain of distinct conditions, a chain
    /// whose repeated condition calls a method and is therefore exempt, a switch statement whose labels differ,
    /// and a switch expression whose arms differ. Exactly zero diagnostics.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public bool Check(int value) => value > 0;

               public int Classify(int value)
               {
                   if (value < 0)
                   {
                       return -1;
                   }
                   else if (value == 0)
                   {
                       return 0;
                   }
                   else if (value > 10)
                   {
                       return 2;
                   }
                   else
                   {
                       return 1;
                   }
               }

               public int Guarded(int value)
               {
                   if (Check(value))
                   {
                       return 1;
                   }
                   else if (Check(value))
                   {
                       return 2;
                   }

                   return 0;
               }

               public int Route(int value, bool flag)
               {
                   switch (value)
                   {
                       case 1:
                           return 10;
                       case 2 when flag:
                           return 20;
                       case 3:
                           return 30;
                       default:
                           return 0;
                   }
               }

               public string Name(int value) => value switch
               {
                   1 => "one",
                   2 => "two",
                   _ => "other",
               };
           }
           """;

    /// <summary>Builds one type whose repeated conditions are all reported.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Three diagnostics per block: the repeated chain condition, the repeated case label, and the repeated switch-expression arm.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Classify(int value)
               {
                   if (value < 0)
                   {
                       return -1;
                   }
                   else if (value > 10)
                   {
                       return 2;
                   }
                   else if (value < 0)
                   {
                       return 3;
                   }

                   return 0;
               }

               public int Route(int value, bool flag)
               {
                   switch (value)
                   {
                       case 1 when flag:
                           return 10;
                       case 2:
                           return 20;
                       case 1 when flag:
                           return 30;
                       default:
                           return 0;
                   }
               }

               public string Name(int value, bool flag) => value switch
               {
                   1 when flag => "one",
                   2 => "two",
                   1 when flag => "uno",
                   _ => "other",
               };
           }
           """;
}
