// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for line-length analyzer benchmarks.</summary>
internal static class LineTooLongBenchmarkSource
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

    /// <summary>Builds one type whose every line fits inside the default maximum.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string Name { get; set; } = "value";

               public string Describe(string prefix) => prefix + Name;

               public int Length(string value) => value.Length + Name.Length;
           }
           """;

    /// <summary>Builds one type carrying two over-long lines, each of which could be wrapped at whitespace.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public string Name { get; set; } = "value";

               public string Describe(string a, string b, string c) => a + b + c + Name + a + b + c + Name + a + b + c + Name + a + b + c + Name + a + Name;

               public int Length(string a, string b, string c) => a.Length + b.Length + c.Length + Name.Length + a.Length + b.Length + c.Length + Name.Length;
           }
           """;
}
