// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for never-raised-event analyzer benchmarks (SST2407).</summary>
internal static class EventNeverRaisedBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating events.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    /// <remarks>
    /// Each type's event is given its own name. The compilation-wide index of names is name-based, so a
    /// shared name would let one type's raise silence every other type's event and make the corpus lie.
    /// </remarks>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type that raises the event it declares.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public event EventHandler Started{{index}};

               public event EventHandler Stopped{{index}};

               public void Start() => Started{{index}}?.Invoke(this, EventArgs.Empty);

               public void Stop()
               {
                   var handler = Stopped{{index}};
                   handler?.Invoke(this, EventArgs.Empty);
               }
           }
           """;

    /// <summary>Builds one type whose event nothing raises.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public event EventHandler Started{{index}};
           }
           """;
}
