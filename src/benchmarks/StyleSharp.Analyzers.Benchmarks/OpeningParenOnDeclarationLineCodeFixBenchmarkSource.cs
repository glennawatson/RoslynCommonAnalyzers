// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1110 code-fix benchmarks.</summary>
internal static class OpeningParenOnDeclarationLineCodeFixBenchmarkSource
{
    /// <summary>Builds a compilation unit containing many SST1110 violations of the requested shape.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="bracketedArgument">Whether to generate bracketed-argument violations instead of method-parameter violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool bracketedArgument)
        => $$"""
           namespace Bench;
           internal static class OpeningParenCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => bracketedArgument ? GenerateBracketedArgumentMember(i) : GenerateMethodParameterMember(i))}}
           }
           """;

    /// <summary>Builds one method-parameter SST1110 violation.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMethodParameterMember(int index)
        => $$"""
           private static int Add{{index}}
               (int value) => value;
           """;

    /// <summary>Builds one bracketed-argument SST1110 violation.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateBracketedArgumentMember(int index)
        => $$"""
           private static int Use{{index}}(int[] values)
               => values
                   [0];
           """;
}
