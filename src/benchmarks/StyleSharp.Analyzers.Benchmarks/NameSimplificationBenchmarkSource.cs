// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for shortest-equivalent-name analysis.</summary>
internal static class NameSimplificationBenchmarkSource
{
    /// <summary>Builds clean or violating name-simplification members.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable qualified names and <c>this.</c> accesses.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           using System.Text;

           namespace Bench;

           internal sealed class NameSimplificationBench
           {
               private readonly int _value = 1;

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds source where the shorter name is visible through normal lookup.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUnshadowedLookup(int members)
        => $$"""
           using System.Text;

           namespace Bench;

           internal sealed class NameSimplificationLookupBench
           {
               private readonly int _value = 1;

           {{BenchmarkSourceText.JoinBlocks(members, GenerateUnshadowedLookupMember)}}
           }
           """;

    /// <summary>Builds source where shorter names are hidden by nearer declarations.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateShadowedLookup(int members)
        => $$"""
           namespace Other
           {
               internal sealed class Widget
               {
               }
           }

           namespace Bench;

           internal sealed class Widget
           {
           }

           internal sealed class NameSimplificationShadowBench
           {
               private readonly int _value = 1;

           {{BenchmarkSourceText.JoinBlocks(members, GenerateShadowedLookupMember)}}
           }
           """;

    /// <summary>Builds source that keeps generic names on the speculative-binding fallback path.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateGenericFallback(int members)
        => $$"""
           using System.Collections.Generic;

           namespace Bench;

           internal sealed class NameSimplificationGenericBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateGenericFallbackMember)}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index & 1, violating) switch
        {
            (0, true) => $$"""
                           public int Qualified{{index}}(System.Text.StringBuilder builder) => builder.Length;
                           """,
            (1, true) => $$"""
                           public int Member{{index}}() => this._value + {{index}};
                           """,
            (0, false) => $$"""
                            public int Qualified{{index}}(StringBuilder builder) => builder.Length;
                            """,
            _ => $$"""
                   public int Member{{index}}()
                   {
                       var _value = {{index}};
                       return this._value + _value;
                   }
                   """
        };

    /// <summary>Builds one unshadowed lookup member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateUnshadowedLookupMember(int index)
        => (index & 1) == 0
            ? $$"""
                public int Qualified{{index}}(System.Text.StringBuilder builder) => builder.Length;
                """
            : $$"""
                public int Member{{index}}() => this._value + {{index}};
                """;

    /// <summary>Builds one shadowed lookup member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateShadowedLookupMember(int index)
        => (index & 1) == 0
            ? $$"""
                public Other.Widget Qualified{{index}}() => new Other.Widget();
                """
            : $$"""
                public int Member{{index}}()
                {
                    var _value = {{index}};
                    return this._value + _value;
                }
                """;

    /// <summary>Builds one generic fallback member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateGenericFallbackMember(int index)
        => $$"""
           public int Generic{{index}}(System.Collections.Generic.List<int> values) => values.Count + {{index}};
           """;
}
