// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for conservative modern-syntax style analysis.</summary>
internal static class ModernSyntaxStyleBenchmarkSource
{
    /// <summary>The number of modern syntax shapes cycled by this source generator.</summary>
    private const int ModernSyntaxShapeCount = 4;

    /// <summary>The modulus bucket that emits a range candidate.</summary>
    private const int RangeShape = 2;

    /// <summary>The modulus bucket that emits a property-initializer candidate.</summary>
    private const int PropertyInitializerShape = 3;

    /// <summary>Builds clean or violating modern-syntax-style members.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable target-typed-new and from-end-index shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class Person
           {
               public int Value { get; set; }
           }

           internal sealed class ModernSyntaxStyleBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % ModernSyntaxShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public Person Create{{index}}()
                           {
                               Person person = new Person();
                               return person;
                           }
                           """,
            (1, true) => $$"""
                           public int Last{{index}}(int[] values) => values[values.Length - 1];
                           """,
            (RangeShape, true) => $$"""
                                     public string Slice{{index}}(string text, int start, int length) => text.Substring(start, length);
                                     """,
            (PropertyInitializerShape, true) => $$"""
                                                  public Person Owner{{index}} { get; set; } = new Person();
                                                  """,
            (0, false) => $$"""
                            public Person Create{{index}}()
                            {
                                Person person = new();
                                return person;
                            }
                            """,
            (1, false) => $$"""
                            public int Last{{index}}(int[] values) => values[^1];
                            """,
            (RangeShape, false) => $$"""
                                     public string Slice{{index}}(string text, int start, int length) => text[start..(start + length)];
                                     """,
            _ => $$"""
                   public Person Owner{{index}} { get; set; } = new();
                   """
        };
}
