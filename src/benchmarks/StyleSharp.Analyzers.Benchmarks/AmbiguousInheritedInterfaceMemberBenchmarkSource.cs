// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for ambiguous-inherited-interface-member analyzer benchmarks (SST2320).</summary>
internal static class AmbiguousInheritedInterfaceMemberBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or ambiguous interface inheritance.</summary>
    /// <param name="types">The number of synthetic interface groups to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingGroup(i) : GenerateCleanGroup(i))}}
           """;

    /// <summary>Builds one interface whose two base interfaces declare distinct members.</summary>
    /// <param name="index">The synthetic group index.</param>
    /// <returns>The generated interface block.</returns>
    private static string GenerateCleanGroup(int index)
        => $$"""
           public interface ICleanLeft{{index}}
           {
               string Left{{index}} { get; }
           }

           public interface ICleanRight{{index}}
           {
               string Right{{index}} { get; }
           }

           public interface IClean{{index}} : ICleanLeft{{index}}, ICleanRight{{index}}
           {
           }
           """;

    /// <summary>Builds one interface that inherits one member from two unrelated base interfaces.</summary>
    /// <param name="index">The synthetic group index.</param>
    /// <returns>The generated interface block.</returns>
    private static string GenerateViolatingGroup(int index)
        => $$"""
           public interface IViolatingLeft{{index}}
           {
               string Name{{index}} { get; }
           }

           public interface IViolatingRight{{index}}
           {
               string Name{{index}} { get; }
           }

           public interface IViolating{{index}} : IViolatingLeft{{index}}, IViolatingRight{{index}}
           {
           }
           """;
}
