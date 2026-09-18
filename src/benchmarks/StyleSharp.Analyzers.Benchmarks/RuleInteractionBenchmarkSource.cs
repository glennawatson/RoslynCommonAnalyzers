// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Supplies marker, generic inheritance, deprecation and caching inputs.</summary>
internal static class RuleInteractionBenchmarkSource
{
    /// <summary>Builds one complete application corpus.</summary>
    /// <param name="rule">The diagnostic identifier.</param>
    /// <param name="nodes">The number of application types.</param>
    /// <param name="violating">Whether the explicitly enabled rule should report.</param>
    /// <returns>The source text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Generate(string rule, int nodes, bool violating) =>
        BenchmarkSourceText.JoinBlocks(nodes, index => GenerateType(rule, index, violating));

    /// <summary>Builds an independently compilable interaction example.</summary>
    /// <param name="rule">The diagnostic identifier.</param>
    /// <param name="index">The unique type index.</param>
    /// <param name="violating">Whether the rule should report.</param>
    /// <returns>The declaration source.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rule has no corpus.</exception>
    private static string GenerateType(string rule, int index, bool violating) => rule switch
    {
        "SST1436" => $$"""
            namespace System.Runtime.CompilerServices
            {
                internal static class {{(index == 0 ? "IsExternalInit" : $"Marker{index}")}}
                {
                    {{(violating ? string.Empty : "public static int Value => 1;")}}
                }
            }
            """,
        "SST1452" => $$"""
            public class Runner{{index}}<T> where T : {{(violating ? "class" : $"Runner{index}<T>")}}, new()
            {
                public int Value => 1;
            }
            public class Schema{{index}} : Runner{{index}}<Schema{{index}}> { }
            """,
        "SST1496" => $$"""
            public {{(violating ? "abstract " : string.Empty)}}class Runner{{index}}<T> where T : Runner{{index}}<T>, new()
            {
                public int Value => 1;
            }
            public class Schema{{index}} : Runner{{index}}<Schema{{index}}> { }
            """,
        "SST2310" => $$"""
            public class Library{{index}}
            {
                {{(violating ? "[System.Obsolete(\"Use Replacement.\", DiagnosticId = \"LIB0001\", UrlFormat = \"https://example.invalid/{0}\")]" : "[System.CLSCompliant(false)]")}}
                public int Legacy() => 1;
                public int Replacement() => 1;
            }
            """,
        "SST1420" => $$"""
            public class Catalog{{index}}
            {
                {{(violating ? "private readonly int[] _items = new[] { 1, 2 }; public int[] Items => _items;" : "public int[] Items { get; } = new[] { 1, 2 };")}}
            }
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };
}
