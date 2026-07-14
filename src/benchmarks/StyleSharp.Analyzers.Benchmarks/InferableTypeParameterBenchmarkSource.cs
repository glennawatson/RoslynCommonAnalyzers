// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for uninferable-type-parameter analysis (SST2307).</summary>
internal static class InferableTypeParameterBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises inferable or uninferable type parameters.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Collections.Generic;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose type parameters a call site never has to name.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a method with no type parameters at all,
    /// which is what nearly every method in a real file hits and costs one array-length check; a type
    /// parameter written directly as a parameter; one buried inside a constructed type, which is the case
    /// that pays for the recursive walk; and one that cannot be inferred but is not externally visible.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public static class C{{index}}
           {
               public static int Length(string text) => text.Length;

               public static void Take<T>(T value) => Console.WriteLine(value);

               public static T Echo<T>(T value) => value;

               public static void Fill<T>(List<T> items, Func<int, T> factory) => items.Add(factory({{index}}));

               internal static void Hidden<TService>() => Console.WriteLine(typeof(TService));
           }
           """;

    /// <summary>Builds one type whose type parameters every call site has to spell out.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public static class V{{index}}
           {
               public static void Register<TService>() => Console.WriteLine(typeof(TService));

               public static T Create<T>() => default!;

               public static void Copy<TSource, TItem>(TSource source)
                   where TSource : IEnumerable<TItem>
                   => Console.WriteLine(source);
           }
           """;
}
