// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for recursive-generic-inheritance analyzer benchmarks (SST2437).</summary>
internal static class RecursiveGenericInheritanceBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating generic base lists.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           """;

    /// <summary>Builds one type whose generic base is the legitimate self-referential shape.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public class Base{{index}}<T>
           {
           }

           public class Fluent{{index}}<T> : Base{{index}}<Fluent{{index}}<T>>
           {
           }
           """;

    /// <summary>Builds one type nested inside its own base's type arguments.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public class Root{{index}}<T>
           {
           }

           public class Recursive{{index}}<T> : Root{{index}}<Recursive{{index}}<Recursive{{index}}<T>>>
           {
           }
           """;
}
