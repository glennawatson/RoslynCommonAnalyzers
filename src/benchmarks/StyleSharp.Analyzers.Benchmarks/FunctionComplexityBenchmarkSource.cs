// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for SST1442/SST1443 function-complexity benchmarks.</summary>
internal static class FunctionComplexityBenchmarkSource
{
    /// <summary>The number of switch-expression arms in the clean table-dispatch case.</summary>
    private const int CleanSwitchArmCount = 24;

    /// <summary>The number of sequential branches needed to exceed the default branch limit.</summary>
    private const int ViolatingBranchCount = 11;

    /// <summary>Builds a compilation unit with clean or violating function-complexity cases.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit threshold violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one synthetic type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit threshold violations.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds a type with wide switch dispatch and shallow guards that stay under thresholds.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class ComplexityClean{{index}}
           {
               public int Dispatch(int value) =>
                   value switch
                   {
           {{GenerateSwitchArms(CleanSwitchArmCount)}}
                       _ => -1
                   };

               public int Shallow(int value)
               {
                   if (value < 0)
                   {
                       return -1;
                   }

                   if (value == 0)
                   {
                       return 0;
                   }

                   return value > 10 ? 10 : value;
               }
           }
           """;

    /// <summary>Builds a type whose branch count and nested-flow count exceed the defaults.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class ComplexityViolation{{index}}
           {
               public int ManyBranches(int value)
               {
                   var result = 0;
           {{GenerateSequentialBranches(ViolatingBranchCount)}}
                   return result;
               }

               public int NestedFlow(int value)
               {
                   var result = 0;
                   if (value > 0)
                   {
                       if (value > 1)
                       {
                           if (value > 2)
                           {
                               if (value > 3)
                               {
                                   if (value > 4)
                                   {
                                       if (value > 5)
                                       {
                                           result = value;
                                       }
                                   }
                               }
                           }
                       }
                   }

                   return result;
               }
           }
           """;

    /// <summary>Builds switch-expression arms.</summary>
    /// <param name="count">The number of arms to emit.</param>
    /// <returns>The generated arm text.</returns>
    private static string GenerateSwitchArms(int count)
        => BenchmarkSourceText.JoinLines(count, i => $"                    {i} => {i},");

    /// <summary>Builds sequential <c>if</c> statements.</summary>
    /// <param name="count">The number of branches to emit.</param>
    /// <returns>The generated branch text.</returns>
    private static string GenerateSequentialBranches(int count)
        => BenchmarkSourceText.JoinLines(count, i => $"        if (value == {i}) result++;");
}
