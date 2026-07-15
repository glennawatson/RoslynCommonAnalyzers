// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for serialization-callback-signature analyzer benchmarks (SST2430).</summary>
internal static class SerializationCallbackSignatureBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating serialization callbacks.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Runtime.Serialization;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose callback has the shape the serializer invokes.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public class C{{index}}
           {
               [OnDeserialized]
               private void AfterLoad{{index}}(StreamingContext context)
               {
               }
           }
           """;

    /// <summary>Builds one type whose callback parameter is not a StreamingContext.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class V{{index}}
           {
               [OnDeserialized]
               private void AfterLoad{{index}}(int version)
               {
               }
           }
           """;
}
