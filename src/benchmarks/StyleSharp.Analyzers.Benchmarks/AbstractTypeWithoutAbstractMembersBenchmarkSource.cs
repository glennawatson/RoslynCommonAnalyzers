// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for abstract-type analyzer benchmarks.</summary>
internal static class AbstractTypeWithoutAbstractMembersBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating abstract types.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit abstract types with nothing abstract in them.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating group of types.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating group.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one group of types that are all rejected.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route: an ordinary class the symbol filter drops immediately, a static class
    /// (abstract and sealed in metadata), an abstract class that declares its own abstract member, and an
    /// abstract class that inherits one it leaves unimplemented — the only route that walks the base chain.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class Concrete{{index}}
           {
               public int Value { get; set; }
           }

           public static class Helpers{{index}}
           {
               public static int Twice(int value) => value * 2;
           }

           public abstract class Shape{{index}}
           {
               public abstract double Area { get; }

               public double Half() => Area / 2;
           }

           public abstract class Rounded{{index}} : Shape{{index}}
           {
               public double Radius { get; set; }
           }
           """;

    /// <summary>Builds one group of abstract types that declare nothing abstract.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Emits two reported types: one deriving from object, one whose base chain is fully implemented.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public abstract class Helper{{index}}
           {
               public int Add(int left, int right) => left + right;
           }

           public abstract class Shape{{index}}
           {
               public abstract double Area { get; }
           }

           public abstract class Square{{index}} : Shape{{index}}
           {
               public double Side { get; set; }

               public override double Area => Side * Side;
           }
           """;
}
