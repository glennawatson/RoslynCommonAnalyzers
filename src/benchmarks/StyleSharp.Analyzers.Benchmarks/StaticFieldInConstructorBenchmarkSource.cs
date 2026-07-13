// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for static-field-in-constructor analyzer benchmarks (SST2402).</summary>
internal static class StaticFieldInConstructorBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating constructors.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose constructor only sets its own state.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: instance-field writes (the common case,
    /// which must not bind), an accumulating counter, a guarded lazy initializer, and a static constructor.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public static int Created;

               public static string Default = string.Empty;

               private readonly int _value;

               private readonly string _name;

               static C{{index}}() => Default = "none";

               public C{{index}}(int value, string name)
               {
                   _value = value;
                   _name = name;
                   Created++;
                   if (Default.Length == 0)
                   {
                       Default = name;
                   }
               }

               public int Value => _value;

               public string Name => _name;
           }
           """;

    /// <summary>Builds one type whose constructor overwrites a static field.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public static int Latest;

               private readonly int _value;

               public V{{index}}(int value)
               {
                   _value = value;
                   Latest = value;
               }

               public int Value => _value;
           }
           """;
}
