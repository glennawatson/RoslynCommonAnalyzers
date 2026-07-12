// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for duplicated-string-literal analyzer benchmarks.</summary>
/// <remarks>
/// SST1486 counts per file and the whole corpus is one file, so every literal a clean type emits must either
/// be unique to that type or land on an exclusion. The type index is woven into the countable literals for
/// exactly that reason: without it, a hundred copies of one clean type would be a hundred duplicates.
/// </remarks>
internal static class DuplicatedStringLiteralBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating duplicated-literal patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit duplicated-string-literal rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating duplicated-literal type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose literals are all unique or excluded.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a short literal, a whitespace-only literal, a
    /// const and a static readonly initializer, a repeated attribute argument, a repeated case label, and a
    /// countable literal that is simply unique. The repeated-but-excluded literals are deliberately identical
    /// across every type, so the exclusion walk — not luck — is what keeps the file clean.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private const string Primary = "clean-const-value-{{index}}";

               private static readonly string Alias = "clean-static-readonly-value-{{index}}";

               [System.Obsolete("this member is going away")]
               public string Legacy() => Primary;

               public string Unique() => "clean-unique-value-{{index}}";

               public string Blank() => "      ";

               public string Tiny() => "id";

               public string Rank(string value) => value switch
               {
                   "clean-case-alpha" => Primary,
                   "clean-case-beta" => Alias,
                   _ => Primary,
               };
           }
           """;

    /// <summary>Builds one type whose literal is written three times and is therefore reported once.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public string First() => "violating-value-{{index}}";

               public string Second() => "violating-value-{{index}}";

               public string Third() => "violating-value-{{index}}";
           }
           """;
}
