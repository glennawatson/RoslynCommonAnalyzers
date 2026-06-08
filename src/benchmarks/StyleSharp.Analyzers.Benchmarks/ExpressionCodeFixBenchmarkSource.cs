// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source tailored to expression-oriented direct code-fix benchmarks.</summary>
internal static class ExpressionCodeFixBenchmarkSource
{
    /// <summary>Builds source containing only bare instance-member calls that need a <c>this.</c> prefix.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePrefixLocalCallsWithThis(int members)
        => $$"""
           namespace Bench;

           internal sealed class PrefixLocalCallsWithThisBench
           {
               private int _state;

               private void Helper(int value)
               {
                   this._state = value;
               }

           {{BenchmarkSourceText.JoinBlocks(members, GeneratePrefixLocalCallsWithThisMember)}}
           }
           """;

    /// <summary>Builds source containing only precedence expressions that need parentheses.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePrecedence(int members)
        => $$"""
           namespace Bench;

           internal static class PrecedenceBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GeneratePrecedenceMember)}}
           }
           """;

    /// <summary>Builds one bare instance-member call that needs a <c>this.</c> prefix.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GeneratePrefixLocalCallsWithThisMember(int index)
        => $$"""
           internal int M{{index}}(int value, int other)
           {
               Helper(value);
               return value + other + _state;
           }
           """;

    /// <summary>Builds one precedence expression that needs parentheses.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GeneratePrecedenceMember(int index)
        => $$"""
           internal static int M{{index}}(int value, int other)
           {
               return value + other << 1;
           }
           """;
}
