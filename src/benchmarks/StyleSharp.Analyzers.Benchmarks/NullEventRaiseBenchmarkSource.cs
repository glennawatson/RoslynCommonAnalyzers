// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for null-event-raise analyzer benchmarks (SST2436).</summary>
internal static class NullEventRaiseBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating event raises.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           """;

    /// <summary>Builds one type that raises its event with this and empty args.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public event EventHandler Changed;

               public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
           }
           """;

    /// <summary>Builds one type that raises its event with a null sender.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public event EventHandler Changed;

               public void Raise() => Changed?.Invoke(null, EventArgs.Empty);
           }
           """;
}
