// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST2338 prefer-union analyzer benchmarks.</summary>
internal static class Sst2338PreferUnionBenchmarkSource
{
    /// <summary>The union marker the rule gates on, declared once per compilation.</summary>
    private const string Marker = "namespace System.Runtime.CompilerServices { public interface IUnion { } }";

    /// <summary>Builds a compilation unit of clean or violating tagged types.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit prefer-union rule violations.</param>
    /// <returns>The generated source text.</returns>
    internal static string Generate(int types, bool violating) =>
        $$"""
           #nullable enable
           {{Marker}}

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating tagged type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating) =>
        violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type the rule leaves alone.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers the routes the no-diagnostic path takes: a single payload beside a tag, a tag-free type, and
    /// the value-typed payloads the rule withholds because a union would box them.
    /// </remarks>
    private static string GenerateCleanType(int index) =>
        $$"""
           public enum CleanKind{{index}}
           {
               Text,
               Count,
           }

           public sealed class CleanSingle{{index}}
           {
               public CleanKind{{index}} Kind { get; set; }

               public string? Text { get; set; }
           }

           public sealed class CleanBoxing{{index}}
           {
               public CleanKind{{index}} Kind { get; set; }

               public int? Count { get; set; }

               public double? Ratio { get; set; }
           }

           public sealed class CleanUntagged{{index}}
           {
               public string? Text { get; set; }

               public byte[]? Bytes { get; set; }
           }
           """;

    /// <summary>Builds one type reported as a hand-rolled union.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index) =>
        $$"""
           public enum PayloadKind{{index}}
           {
               Text,
               Bytes,
           }

           public sealed class Payload{{index}}
           {
               public PayloadKind{{index}} Kind { get; set; }

               public string? Text { get; set; }

               public byte[]? Bytes { get; set; }
           }
           """;
}
