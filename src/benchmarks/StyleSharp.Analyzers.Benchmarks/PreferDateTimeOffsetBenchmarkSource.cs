// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for public-boundary date-and-time analysis (SST2016).</summary>
internal static class PreferDateTimeOffsetBenchmarkSource
{
    /// <summary>Builds a compilation unit whose boundaries either carry an offset or lose one.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose visible surface keeps the offset.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers both rejection routes the no-diagnostic path takes: the spelling test, which rejects every
    /// declaration whose type is not written <c>DateTime</c> without binding anything — nearly every
    /// declaration in a real file — and a <c>DateTime</c> that does get bound but stays inside the assembly.
    /// The local exists to show the rule never looks at an expression: a clock read is SST2010's business.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public class C{{index}}
           {
               private DateTime _cached;

               public DateTimeOffset Created;

               public C{{index}}(DateTimeOffset created) => Created = created;

               public DateTimeOffset Modified { get; set; }

               public DateTimeOffset Round(DateTimeOffset value) => value;

               public int Index => {{index}};

               public DateTimeOffset Snapshot()
               {
                   var local = DateTime.UtcNow;
                   _cached = local;
                   return new DateTimeOffset(_cached);
               }
           }

           public delegate DateTimeOffset Clock{{index}}();
           """;

    /// <summary>Builds one type whose visible surface hands out a moment with no offset attached.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class V{{index}}
           {
               public DateTime Created;

               public V{{index}}(DateTime created) => Created = created;

               public DateTime Modified { get; set; }

               public DateTime Round(DateTime value) => value;

               public int Index => {{index}};
           }

           public delegate DateTime Stamp{{index}}();
           """;
}
