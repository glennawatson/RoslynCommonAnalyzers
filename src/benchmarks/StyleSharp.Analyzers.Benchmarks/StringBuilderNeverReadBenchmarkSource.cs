// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for unread-string-builder analyzer benchmarks (SST2408).</summary>
internal static class StringBuilderNeverReadBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating builders.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Text;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose builders are all read.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route: a builder collected with <c>ToString</c>, one handed to something else,
    /// one whose property is read, and a local of another type entirely — which the name comparison must
    /// reject before it binds.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string Join(string[] lines)
               {
                   var builder = new StringBuilder();
                   foreach (var line in lines)
                   {
                       builder.AppendLine(line);
                   }

                   return builder.ToString();
               }

               public void Measure(string[] lines)
               {
                   var builder = new StringBuilder();
                   var counter = 0;
                   foreach (var line in lines)
                   {
                       builder.Append(line);
                       counter++;
                   }

                   Console.WriteLine(builder.Length + counter);
                   Write(builder);
               }

               private static void Write(StringBuilder builder)
               {
               }
           }
           """;

    /// <summary>Builds one type whose builder is filled and thrown away.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Join(string[] lines)
               {
                   var builder = new StringBuilder();
                   foreach (var line in lines)
                   {
                       builder.AppendLine(line);
                   }
               }
           }
           """;
}
