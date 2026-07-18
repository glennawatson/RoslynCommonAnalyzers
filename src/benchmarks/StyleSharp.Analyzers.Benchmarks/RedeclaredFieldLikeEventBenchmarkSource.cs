// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for redeclared field-like event analyzer benchmarks (SST2456).</summary>
internal static class RedeclaredFieldLikeEventBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating event declarations.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose events all keep a single subscriber list.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a plain field-like event and a virtual one
    /// (the common cases, rejected by the modifier scan), an abstract event, an event with explicit accessors
    /// (never visited — it is not a field-like declaration), and a <c>new</c> that hides nothing (rejected
    /// after the base-type walk finds no inherited event).
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public abstract class C{{index}}
           {
               private EventHandler _completed;

               public event EventHandler Changed;

               public virtual event EventHandler Watched;

               public abstract event EventHandler Started;

               public virtual event EventHandler Completed
               {
                   add => _completed += value;
                   remove => _completed -= value;
               }

               public new event EventHandler Spurious;

               public void Raise()
               {
                   Changed?.Invoke(this, EventArgs.Empty);
                   Watched?.Invoke(this, EventArgs.Empty);
                   Completed?.Invoke(this, EventArgs.Empty);
                   Spurious?.Invoke(this, EventArgs.Empty);
               }
           }
           """;

    /// <summary>Builds one type family whose derived event redeclarations split the subscriber list.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class B{{index}}
           {
               public virtual event EventHandler Changed;

               public event EventHandler Hidden;

               protected void Raise()
               {
                   Changed?.Invoke(this, EventArgs.Empty);
                   Hidden?.Invoke(this, EventArgs.Empty);
               }
           }

           public class V{{index}} : B{{index}}
           {
               public override event EventHandler Changed;

               public new event EventHandler Hidden;

               public void RaiseDerived()
               {
                   Changed?.Invoke(this, EventArgs.Empty);
                   Hidden?.Invoke(this, EventArgs.Empty);
               }
           }
           """;
}
