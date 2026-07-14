// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for deprecated-code analysis (SST2310).</summary>
internal static class ObsoleteRemovalBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises deprecated or current types.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Diagnostics;

           namespace Bench;

           [AttributeUsage(AttributeTargets.All)]
           public sealed class VendorObsoleteAttribute : Attribute
           {
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type that deprecates nothing.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The clean corpus is the one that matters here: every rule this analyzer has to reject is an
    /// attribute that is not the framework's obsolete attribute. It covers the two routes — a name that
    /// does not match at all (the token test, which is what nearly every attribute in a real file hits),
    /// and a name that does match but binds to somebody else's type.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           [DebuggerDisplay("C{{index}}")]
           public sealed class C{{index}}
           {
               [VendorObsolete]
               public int Legacy { get; set; }

               [Conditional("DEBUG")]
               public void Traced()
               {
               }

               [DebuggerStepThrough]
               public void Stepped()
               {
               }
           }
           """;

    /// <summary>Builds one type that is deprecated, however well it is documented.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// A message does not exempt the attribute, so the violating corpus mixes bare, explained, and fully
    /// specified deprecations: all of them are reported, and all of them cost the same bind.
    /// </remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           [Obsolete("Use W{{index}} instead.")]
           public sealed class V{{index}}
           {
               [Obsolete]
               public int Legacy { get; set; }

               [Obsolete("Use Run(RunOptions) instead.")]
               public void Explained()
               {
               }

               [Obsolete("Gone in 5.0.", true)]
               public void Fatal()
               {
               }

               [Obsolete("Use Run(RunOptions).", DiagnosticId = "BENCH001")]
               public void Identified()
               {
               }
           }
           """;
}
