// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for exception-constructor analyzer benchmarks.</summary>
internal static class ExceptionConstructorBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating exception shapes.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit exception types that are missing constructors.</param>
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

    /// <summary>Builds one type the rule never reports.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The plain class is the shape that dominates a real compilation and the one the clean path is tuned
    /// for: it is rejected by the base-type walk without ever reading options or scanning constructors.
    /// The exception beside it declares all three constructors, so it exercises the full scan and still
    /// reports nothing.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class Widget{{index}}
           {
               public int Value { get; set; }
           }

           public class CleanException{{index}} : System.Exception
           {
               public CleanException{{index}}()
               {
               }

               public CleanException{{index}}(string message)
                   : base(message)
               {
               }

               public CleanException{{index}}(string message, System.Exception innerException)
                   : base(message, innerException)
               {
               }
           }
           """;

    /// <summary>Builds one exception type that declares none of the expected constructors.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Yields exactly one SST1488 per type; the plain class beside it is never reported.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class Widget{{index}}
           {
               public int Value { get; set; }
           }

           public class BadException{{index}} : System.Exception
           {
           }
           """;
}
