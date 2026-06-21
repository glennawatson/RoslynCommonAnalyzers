// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the grouped language-style readability benchmarks.</summary>
internal static class LanguageStyleBenchmarkSource
{
    /// <summary>The number of violating language-style shapes cycled by the source generator.</summary>
    private const int LanguageStyleShapeCount = 7;

    /// <summary>The modulus bucket that emits a null-coalescing candidate.</summary>
    private const int CoalesceShape = 2;

    /// <summary>The modulus bucket that emits a null-propagation candidate.</summary>
    private const int PropagationShape = 3;

    /// <summary>The modulus bucket that emits a conditional-return candidate.</summary>
    private const int ConditionalReturnShape = 4;

    /// <summary>The modulus bucket that emits a conditional-assignment candidate.</summary>
    private const int ConditionalAssignmentShape = 5;

    /// <summary>The modulus bucket that emits a <c>nameof</c> candidate.</summary>
    private const int NameofShape = 6;

    /// <summary>Builds a compilation unit of clean or violating style-expression members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable language-style shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           using System.Collections.Generic;

           namespace Bench;

           internal sealed class Person
           {
               public string? Name { get; set; }
           }

           internal sealed class LanguageStyleBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % LanguageStyleShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public Person Object{{index}}()
                           {
                               var person = new Person();
                               person.Name = "{{index}}";
                               return person;
                           }
                           """,
            (1, true) => $$"""
                           public List<int> Collection{{index}}()
                           {
                               var values = new List<int>();
                               values.Add({{index}});
                           return values;
                       }
                       """,
            (CoalesceShape, true) => $$"""
                                        public string Coalesce{{index}}(string? value, string fallback) => value == null ? fallback : value;
                                        """,
            (PropagationShape, true) => $$"""
                                           public string? Propagate{{index}}(Person? person) => person == null ? null : person.Name;
                                           """,
            (ConditionalReturnShape, true) => $$"""
                                                 public int Return{{index}}(bool flag)
                                                 {
                                                     if (flag)
                                                     {
                                                         return {{index}};
                                                     }

                                                     return -1;
                                                 }
                                                 """,
            (ConditionalAssignmentShape, true) => $$"""
                                                     public int Assign{{index}}(bool flag)
                                                     {
                                                         var value = 0;
                                                         if (flag)
                                                         {
                                                             value = {{index}};
                                                         }
                                                         else
                                                         {
                                                             value = -1;
                                                         }

                                                         return value;
                                                     }
                                                     """,
            (NameofShape, true) => $$"""
                                      public string Name{{index}}() => typeof(Person).Name;
                                      """,
            _ => $$"""
                   public string Clean{{index}}(Person person) => person.Name ?? "{{index}}";
                   """
        };
}
