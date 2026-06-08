// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for built-in-type-alias analyzer benchmarks.</summary>
internal static class BuiltInTypeAliasBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating type-alias patterns.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit built-in-type-alias violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           using System;

           namespace Bench;

           internal sealed class C
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds a direct-code-fix corpus that targets only method return types.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCodeFixSource(int members)
        => $$"""
           namespace Bench;

           internal sealed class C
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateCodeFixMember)}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violation.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating ? GenerateViolatingMember(index) : GenerateCleanMember(index);

    /// <summary>Builds one clean member that already uses keyword aliases.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCleanMember(int index)
        => $$"""
           internal int M{{index}}(int value)
           {
               int local = value + {{index}};
               return local;
           }
           """;

    /// <summary>Builds one violating member that names framework types directly.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           internal System.Int32 M{{index}}(Int32 value)
           {
               System.Int32 local = value + {{index}};
               return local;
           }
           """;

    /// <summary>Builds one direct-code-fix benchmark member that violates only on the return type.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCodeFixMember(int index)
        => $$"""
           internal System.Int32 M{{index}}()
           {
               return {{index}};
           }
           """;
}
