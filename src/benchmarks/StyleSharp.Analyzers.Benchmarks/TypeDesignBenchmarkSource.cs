// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the type-design analyzer benchmarks.</summary>
internal static class TypeDesignBenchmarkSource
{
    /// <summary>Builds a compilation unit of clean or violating type declarations.</summary>
    /// <param name="members">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit abstract types with public constructors.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating block: an SST1428 constructor case and an SST1431 generic-static case.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit the violating variant of each case.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The generic type exercises SST1431: the clean variant's static members all reference the type
    /// parameter (signature, <c>typeof(T)</c> initializer, and the closed self-type), so the whole-member
    /// type-parameter scan runs to completion; the violating variant's static members ignore it, driving
    /// the report path — including the semantic owner-type check and editorconfig lookup for the fields.
    /// </remarks>
    private static string GenerateType(int index, bool violating)
        => violating
            ? $$"""
              internal abstract class Bench{{index}} { public Bench{{index}}() { } }
              internal sealed class BenchGeneric{{index}}<T>
              {
                  private int _instance;
                  public static int Counter;
                  public static int Add(int a, int b) => a + b;
                  public static readonly System.Collections.Generic.Dictionary<string, string> Map = new();
              }
              """
            : $$"""
              internal sealed class Bench{{index}} { public Bench{{index}}() { } }
              internal sealed class BenchGeneric{{index}}<T>
              {
                  private int _instance;
                  public static T Create() => default!;
                  public static readonly string Name = typeof(T).Name;
                  public static readonly string Key = typeof(BenchGeneric{{index}}<T>).FullName;
              }
              """;
}
