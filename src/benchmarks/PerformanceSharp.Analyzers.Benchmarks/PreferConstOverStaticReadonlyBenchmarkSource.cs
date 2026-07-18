// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for prefer-const-over-static-readonly analyzer benchmarks.</summary>
internal static class PreferConstOverStaticReadonlyBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating constant-field patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit prefer-const rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating constant-field-and-local type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The violating variant carries one constant field and one never-reassigned constant local
    /// (two diagnostics per type); the clean variant declares both as <c>const</c> and keeps a
    /// reassigned, non-constant local to exercise the analyzer's local reject paths.
    /// </remarks>
    private static string GenerateType(int index, bool violating)
        => violating
            ? $$"""
              public sealed class C{{index}}
              {
                  private static readonly int MaxRetries = 3;

                  public int Read() => MaxRetries;

                  public int Compute(int value)
                  {
                      int scale = 10;
                      int total = value * scale;
                      total += MaxRetries;
                      return total;
                  }
              }
              """
            : $$"""
              public sealed class C{{index}}
              {
                  private const int MaxRetries = 3;

                  public int Read() => MaxRetries;

                  public int Compute(int value)
                  {
                      const int Scale = 10;
                      int total = value * Scale;
                      total += MaxRetries;
                      return total;
                  }
              }
              """;
}
