// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for goto analyzer benchmarks.</summary>
internal static class AvoidGotoBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises the clean or the violating shape.</summary>
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

    /// <summary>Builds one type whose only jumps are between switch sections, which are never reported.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Handle(int state)
               {
                   switch (state)
                   {
                       case 1:
                           goto case 2;

                       case 2:
                           goto default;

                       default:
                           return 0;
                   }
               }

               public int Loop(int[] values)
               {
                   foreach (var value in values)
                   {
                       if (value == 0)
                       {
                           continue;
                       }

                       return value;
                   }

                   return 0;
               }
           }
           """;

    /// <summary>Builds one type with two jumps to a label.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Handle(int[] values)
               {
                   foreach (var value in values)
                   {
                       if (value < 0)
                       {
                           goto Failed;
                       }

                       if (value > 100)
                       {
                           goto Failed;
                       }
                   }

                   return 0;

               Failed:
                   return -1;
               }
           }
           """;
}
