// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Builds synthetic source for never-thrown-exception analysis. The clean corpus produces no diagnostics;
/// the violating corpus produces exactly three per type, so a run of N types reports 3N.
/// </summary>
internal static class ExceptionNeverThrownBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating exception creations.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit never-thrown-exception rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating exception-creation type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose exceptions are all consumed.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers both rejection routes: the exception factories, which the parent check rejects without binding,
    /// and a non-exception constructed as a statement, which is the only shape that reaches the semantic model
    /// on the clean path.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public Exception Build() => new InvalidOperationException("build{{index}}");

               public Exception Assign()
               {
                   var error = new InvalidOperationException("assign{{index}}");
                   return error;
               }

               public void Pass() => Fail(new InvalidOperationException("pass{{index}}"));

               public void Guard(string value)
               {
                   if (value is null)
                   {
                       throw new ArgumentNullException(nameof(value));
                   }
               }

               public void Scratch()
               {
                   new System.Text.StringBuilder();
               }

               private static void Fail(Exception error)
               {
               }
           }
           """;

    /// <summary>Builds one type whose exceptions are all constructed and then discarded.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Guard(string value)
               {
                   if (value is null)
                   {
                       new ArgumentNullException(nameof(value));
                   }
               }

               public void Range(int value)
               {
                   if (value < 0)
                   {
                       new ArgumentOutOfRangeException(nameof(value));
                   }
               }

               public void State()
               {
                   new InvalidOperationException("state{{index}}");
               }
           }
           """;
}
