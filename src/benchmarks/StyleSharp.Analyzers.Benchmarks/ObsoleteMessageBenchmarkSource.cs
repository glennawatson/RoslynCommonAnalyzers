// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for unexplained-obsolete-attribute analysis (SST2308).</summary>
internal static class ObsoleteMessageBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating obsolete attributes.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Diagnostics;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose attributes all explain themselves, or are not the attribute at all.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: an unrelated attribute (the name test,
    /// which is what nearly every attribute in a real file hits), a positional message, a named message,
    /// a message plus the error flag, and a message supplied by a constant.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           [DebuggerDisplay("C{{index}}")]
           public sealed class C{{index}}
           {
               private const string Migration = "Use Run(RunOptions) instead.";

               [Obsolete("Use Value instead.")]
               public int Legacy { get; set; }

               [Obsolete(message: "Use Run(RunOptions) instead.")]
               public void Named()
               {
               }

               [Obsolete("Use Run(RunOptions) instead.", true)]
               public void WithError()
               {
               }

               [Obsolete(Migration)]
               public void FromConstant()
               {
               }

               [Conditional("DEBUG")]
               public void Traced()
               {
               }
           }
           """;

    /// <summary>Builds one type whose obsolete attributes explain nothing.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           [Obsolete]
           public sealed class V{{index}}
           {
               [Obsolete]
               public int Legacy { get; set; }

               [Obsolete(null)]
               public void Fatal()
               {
               }

               [Obsolete("")]
               public void Empty()
               {
               }

               [Obsolete("   ")]
               public void Blank()
               {
               }
           }
           """;
}
