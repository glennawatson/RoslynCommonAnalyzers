// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for swapped-argument analyzer benchmarks (SST2400).</summary>
internal static class SwappedArgumentsBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating call sites.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose calls are all in the parameters' order.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route: an ordered call, a named-argument call, a call whose arguments are not
    /// bare identifiers, and a call whose only identifier is already in the right place.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public void Copy(string source, string target)
               {
               }

               public void Run(string source, string target)
               {
                   Copy(source, target);
                   Copy(target: target, source: source);
                   Copy(source.Trim(), target.Trim());
                   Copy(source, target.Trim());
               }
           }
           """;

    /// <summary>Builds one type whose call transposes two arguments.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Copy(string source, string target)
               {
               }

               public void Run(string source, string target)
               {
                   Copy(target, source);
               }
           }
           """;
}
