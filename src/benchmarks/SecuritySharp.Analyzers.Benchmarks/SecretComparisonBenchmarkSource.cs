// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Builds ordinary and secret-bearing operands in the same comparison shapes.</summary>
internal static class SecretComparisonBenchmarkSource
{
    /// <summary>The number of reportable comparisons in each violating type.</summary>
    internal const int ComparisonsPerType = 13;

    /// <summary>Builds comparison types plus the assertion call shape from the reported header example.</summary>
    /// <param name="types">The number of generated comparison types.</param>
    /// <param name="violating">Whether operands include an explicit secret fragment.</param>
    /// <returns>The generated source text.</returns>
    internal static string Generate(int types, bool violating) =>
        $$"""
        using System;
        using System.Linq;
        namespace Bench;
        public static class Assert
        {
            public static void That(string actual, string constraint) { }
        }
        public static class Is
        {
            public static string EqualTo(string expected) => expected;
        }
        {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
        """;

    /// <summary>Builds operators, static and instance comparisons, spans, and a local function.</summary>
    /// <param name="index">The generated type index.</param>
    /// <param name="violating">Whether an operand includes an explicit secret fragment.</param>
    /// <returns>The generated comparison type.</returns>
    private static string GenerateType(int index, bool violating)
    {
        var actual = violating ? "actualToken" : "actual";
        return $$"""
            public class C{{index}}
            {
                public void ValidateHeaders(string[] actualHeaders, string[] expectedHeaders)
                {
                    for (var i = 0; i < actualHeaders.Length; i++)
                    {
                        var {{actual}} = actualHeaders[i];
                        var expected = expectedHeaders[i];
                        _ = {{actual}} == expected;
                        Assert.That({{actual}}, Is.EqualTo(expected));
                    }
                }

                public bool CheckStrings(string {{actual}}, string expected) =>
                    {{actual}} == expected
                    || {{actual}} != expected
                    || {{actual}}.Equals(expected)
                    || string.Equals({{actual}}, expected, StringComparison.Ordinal)
                    || object.Equals({{actual}}, expected)
                    || {{actual}}.SequenceEqual(expected)
                    || Enumerable.SequenceEqual({{actual}}, expected);

                public bool VerifyBuffers(byte[] {{actual}}, byte[] expected) =>
                    {{actual}}.SequenceEqual(expected) || Enumerable.SequenceEqual({{actual}}, expected);

                public bool CompareSpans(ReadOnlySpan<byte> {{actual}}, ReadOnlySpan<byte> expected) =>
                    {{actual}}.SequenceEqual(expected);

                public bool MatchSpans(Span<byte> {{actual}}, Span<byte> expected) =>
                    {{actual}}.SequenceEqual(expected);

                public bool Run(string {{actual}}, string expected)
                {
                    bool Validate() => {{actual}} == expected;
                    return Validate();
                }
            }
            """;
    }
}
