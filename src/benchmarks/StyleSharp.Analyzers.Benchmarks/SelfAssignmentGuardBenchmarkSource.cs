// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for self-assignment-guard analyzer benchmarks.</summary>
internal static class SelfAssignmentGuardBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating guards.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit guards around the assignment they test.</param>
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

    /// <summary>Builds one type whose guards all decide something.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a guard that wraps more than the assignment,
    /// one that stores a different value, a compound assignment, a guard over a call, and a condition that is
    /// not a comparison at all.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private int _value;

               public int Value { get; set; }

               public void Notify(int value)
               {
                   if (_value != value)
                   {
                       _value = value;
                       Raise();
                   }
               }

               public void Fallback(int value, int other)
               {
                   if (_value != value)
                   {
                       _value = other;
                   }
               }

               public void Add(int value)
               {
                   if (_value != value)
                   {
                       _value += value;
                   }
               }

               public void Refresh()
               {
                   if (_value != Next())
                   {
                       _value = Next();
                   }
               }

               public void Enable(bool on, int value)
               {
                   if (on)
                   {
                       _value = value;
                   }
               }

               private int Next() => 1;

               private void Raise()
               {
               }
           }
           """;

    /// <summary>Builds one type whose three guards each decide nothing.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private int _value;

               public int Value { get; set; }

               public void SetField(int value)
               {
                   if (_value != value)
                   {
                       _value = value;
                   }
               }

               public void SetProperty(int value)
               {
                   if (Value != value)
                   {
                       Value = value;
                   }
               }

               public void SetInverted(int value)
               {
                   if (_value == value)
                   {
                   }
                   else
                   {
                       _value = value;
                   }
               }
           }
           """;
}
