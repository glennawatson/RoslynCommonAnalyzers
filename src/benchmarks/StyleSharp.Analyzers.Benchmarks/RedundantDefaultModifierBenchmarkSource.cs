// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for redundant-default-modifier analyzer benchmarks.</summary>
internal static class RedundantDefaultModifierBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating modifier lists.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit modifiers that restate the declaration's default.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating group of declarations.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit violating declarations.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds declarations whose modifiers all carry meaning.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: an interface member with no modifier at all,
    /// a private interface member, a static interface member that keeps abstract, a mutable struct whose member
    /// really is readonly, and an ordinary class whose modifiers say something.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public interface IClean{{index}}
           {
               int Value { get; }

               void Run();

               private int Compute() => 1;
           }

           public interface ICounter{{index}}<TSelf>
               where TSelf : ICounter{{index}}<TSelf>
           {
               static abstract TSelf Zero { get; }
           }

           public struct Mutable{{index}}
           {
               private int _x;

               public readonly int X => _x;

               public void Move(int x) => _x = x;
           }

           public sealed class Ordinary{{index}}
           {
               public int Value => 0;

               private int Compute() => 1;
           }
           """;

    /// <summary>Builds declarations that restate five defaults between them.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public interface IWide{{index}}
           {
               public int Value { get; }

               public abstract void Run();

               virtual int Fallback() => 0;
           }

           public readonly struct Point{{index}}
           {
               public readonly int X => 0;
           }
           """;
}
