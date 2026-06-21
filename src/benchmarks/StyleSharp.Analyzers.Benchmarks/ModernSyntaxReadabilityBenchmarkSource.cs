// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for modern syntax readability analysis.</summary>
internal static class ModernSyntaxReadabilityBenchmarkSource
{
    /// <summary>The number of modern readability shapes cycled by this source generator.</summary>
    private const int ModernReadabilityShapeCount = 6;

    /// <summary>The bucket that emits a tuple deconstruction candidate.</summary>
    private const int DeconstructionShape = 2;

    /// <summary>The bucket that emits a local swap candidate.</summary>
    private const int SwapShape = 3;

    /// <summary>The bucket that emits an inferred tuple element candidate.</summary>
    private const int TupleNameShape = 4;

    /// <summary>The bucket that emits a hash-code candidate.</summary>
    private const int HashCodeShape = 5;

    /// <summary>Builds clean or violating modern-syntax-readability members.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           using System;

           namespace Bench;

           internal sealed class Person
           {
               public string Name { get; set; } = "";

               public int Age { get; set; }
           }

           internal sealed class ModernSyntaxReadabilityBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating ? GenerateViolatingMember(index) : GenerateCleanMember(index);

    /// <summary>Builds one synthetic violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => (index % ModernReadabilityShapeCount) switch
        {
            0 => $$"""
                   public ReadOnlySpan<byte> Header{{index}}() => System.Text.Encoding.UTF8.GetBytes("GET");
                   """,
            1 => $$"""
                   public bool IsInt{{index}}(object value) => value is int _;
                   """,
            DeconstructionShape => $$"""
                                      public string Pair{{index}}()
                                      {
                                          var pair{{index}} = GetPair{{index}}();
                                          var name{{index}} = pair{{index}}.Name;
                                          var age{{index}} = pair{{index}}.Age;
                                          return name{{index}} + age{{index}};
                                      }

                                      private static (string Name, int Age) GetPair{{index}}() => ("Ada", 36);
                                      """,
            SwapShape => $$"""
                           public int Swap{{index}}(int left, int right)
                           {
                               var temp{{index}} = left;
                               left = right;
                               right = temp{{index}};
                               return left + right;
                           }
                           """,
            TupleNameShape => $$"""
                                public (string Name, int Age) Tuple{{index}}(Person person) => (Name: person.Name, Age: person.Age);
                                """,
            HashCodeShape => $$"""
                               private readonly struct HashKey{{index}}
                               {
                                   public int Id { get; init; }

                                   public int Age { get; init; }

                                   public override int GetHashCode() => (Id.GetHashCode() * 397) ^ Age.GetHashCode();
                               }
                               """,
            _ => string.Empty
        };

    /// <summary>Builds one synthetic clean member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCleanMember(int index)
        => (index % ModernReadabilityShapeCount) switch
        {
            0 => $$"""
                   public ReadOnlySpan<byte> Header{{index}}() => "GET"u8;
                   """,
            1 => $$"""
                   public bool IsInt{{index}}(object value) => value is int;
                   """,
            DeconstructionShape => $$"""
                                      public string Pair{{index}}()
                                      {
                                          var (name{{index}}, age{{index}}) = GetPair{{index}}();
                                          return name{{index}} + age{{index}};
                                      }

                                      private static (string Name, int Age) GetPair{{index}}() => ("Ada", 36);
                                      """,
            SwapShape => $$"""
                           public int Swap{{index}}(int left, int right)
                           {
                               (left, right) = (right, left);
                               return left + right;
                           }
                           """,
            TupleNameShape => $$"""
                                public (string Name, int Age) Tuple{{index}}(Person person) => (person.Name, person.Age);
                                """,
            HashCodeShape => $$"""
                               private readonly struct HashKey{{index}}
                               {
                                   public int Id { get; init; }

                                   public int Age { get; init; }

                                   public override int GetHashCode() => System.HashCode.Combine(Id, Age);
                               }
                               """,
            _ => string.Empty
        };
}
