// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the blank-line separation benchmarks (SST1535-SST1537).</summary>
internal static class BlankLineSeparationBenchmarkSource
{
    /// <summary>Builds a compilation unit whose members are correctly or incorrectly spaced.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    /// <remarks>
    /// Each type exercises all three ids at once — a constructor initializer, a conditional operator, and
    /// an expression body — so the measured cost is the whole analyzer rather than one of its callbacks.
    /// </remarks>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           public class BaseType
           {
               public BaseType(int value)
               {
               }
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose blank lines all sit where they belong.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public class Clean{{index}} : BaseType
           {
               public Clean{{index}}()
                   : base({{index}})
               {
               }

               public int Pick{{index}}(bool flag) => flag
                   ? {{index}}
                   : 0;

               public int Run{{index}}(bool flag)
               {
                   if (flag)
                   {
                       return {{index}};
                   }

                   return 0;
               }
           }
           """;

    /// <summary>Builds one type that trips every blank-line rule.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class Violating{{index}} : BaseType
           {
               public Violating{{index}}()
                   :

                   base({{index}})
               {
               }

               public int Pick{{index}}(bool flag) => flag
                   ?

                   {{index}}
                   : 0;

               public int Run{{index}}(bool flag)
               {
                   if (flag)
                   {
                       return {{index}};
                   }
                   return 0;
               }
           }
           """;
}
