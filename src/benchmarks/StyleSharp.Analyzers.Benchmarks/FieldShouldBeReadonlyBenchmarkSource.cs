// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for field-should-be-readonly analyzer benchmarks.</summary>
internal static class FieldShouldBeReadonlyBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating readonly-field patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit readonly-field violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type declaration.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violation.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type with writes outside the constructor.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           internal sealed class C{{index}}
           {
               private int _value;

               internal C{{index}}(int value)
               {
                   _value = value;
               }

               internal void Update(int value)
               {
                   _value = value;
               }
           }
           """;

    /// <summary>Builds one violating type whose field is only assigned in the constructor.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           internal sealed class C{{index}}
           {
               private int _value;

               internal C{{index}}(int value)
               {
                   _value = value;
               }

               internal int GetValue()
               {
                   return _value;
               }
           }
           """;
}
