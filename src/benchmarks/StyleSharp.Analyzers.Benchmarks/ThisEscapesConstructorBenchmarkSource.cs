// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for escaping-this analyzer benchmarks (SST2403).</summary>
internal static class ThisEscapesConstructorBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating constructors.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           public static class Registry
           {
               public static void Add(object item)
               {
               }
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose constructor keeps the object to itself.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route: <c>this</c> as the receiver of a member access, <c>this</c> stored in
    /// the object's own state, and <c>this</c> passed to nothing at all.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly int _value;

               private readonly object _self;

               public C{{index}}(int value)
               {
                   this._value = value;
                   this.Initialize();
                   _self = this;
               }

               public int Value => this._value;

               public object Self => _self;

               private void Initialize()
               {
               }
           }
           """;

    /// <summary>Builds one type whose constructor publishes the half-built object.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private readonly int _value;

               public V{{index}}(int value)
               {
                   _value = value;
                   Registry.Add(this);
               }

               public int Value => _value;
           }
           """;
}
