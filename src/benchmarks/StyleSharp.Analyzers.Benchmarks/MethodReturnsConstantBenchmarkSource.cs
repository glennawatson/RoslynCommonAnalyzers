// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for method-returns-constant analyzer benchmarks.</summary>
internal static class MethodReturnsConstantBenchmarkSource
{
    /// <summary>The base type and interface the generated types take their excluded shapes from.</summary>
    private const string Preamble = """
        public interface IWeighted
        {
            int Weight();
        }

        public abstract class Base
        {
            public virtual int Height() => 1;
        }
        """;

    /// <summary>Builds a compilation unit that exercises clean or violating method bodies.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit methods whose whole body is a constant.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{Preamble}}

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose constant-looking methods are all excluded.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a body that computes, an override, an
    /// interface implementation, a parameterized method, a null guard, and a value that is not a constant.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}} : Base, IWeighted
           {
               private int _value;

               public int Weight() => 1;

               public override int Height() => 2;

               public int Scaled(int factor) => factor;

               public string Find() => null;

               public string Text() => string.Empty;

               public int Value() => _value;

               public int Sum()
               {
                   var total = _value;
                   return total + 1;
               }
           }
           """;

    /// <summary>Builds one type whose four methods each return nothing but a constant.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private const int Max = 10;

               public int Limit() => 5;

               public int Ceiling() => Max;

               public bool IsEnabled() => true;

               public string Name()
               {
                   return "fixed";
               }
           }
           """;
}
