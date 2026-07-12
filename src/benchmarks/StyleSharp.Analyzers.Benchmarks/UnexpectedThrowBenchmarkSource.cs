// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for unexpected-throw analyzer benchmarks.</summary>
internal static class UnexpectedThrowBenchmarkSource
{
    /// <summary>Builds a compilation unit whose implicitly invoked members do or do not throw.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit unexpected-throw rule violations.</param>
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

    /// <summary>Builds one type whose throws are all allowed.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: members whose names are not measured at all
    /// (the common case), measured members that do not throw, an ordinary member that does, and a measured
    /// member whose throw states a deliberate absence.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly int _value;

               public C{{index}}(int value) => _value = value;

               public override bool Equals(object obj) => obj is C{{index}} other && other._value == _value;

               public override int GetHashCode() => _value;

               public override string ToString() => Format(_value);

               public void Dispose()
               {
                   if (_value < 0)
                   {
                       throw new System.NotSupportedException();
                   }
               }

               public int Scale(int factor)
               {
                   if (factor == 0)
                   {
                       throw new System.ArgumentException("factor");
                   }

                   return _value * factor;
               }

               private static string Format(int value) => value.ToString();
           }
           """;

    /// <summary>Builds one type whose implicitly invoked members all throw.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Emits six diagnostics: Equals, GetHashCode, ToString, Dispose, and the two equality operators.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private readonly int _value;

               public V{{index}}(int value) => _value = value;

               public override bool Equals(object obj) => throw new System.InvalidOperationException();

               public override int GetHashCode() => throw new System.InvalidOperationException();

               public override string ToString() => throw new System.InvalidOperationException();

               public void Dispose() => throw new System.InvalidOperationException();

               public static bool operator ==(V{{index}} left, V{{index}} right) => throw new System.InvalidOperationException();

               public static bool operator !=(V{{index}} left, V{{index}} right) => throw new System.InvalidOperationException();
           }
           """;
}
