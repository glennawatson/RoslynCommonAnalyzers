// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for modern-syntax preference analysis.</summary>
internal static class ModernSyntaxPreferenceBenchmarkSource
{
    /// <summary>The number of shapes cycled by the analyzer benchmark corpus.</summary>
    private const int ShapeCount = 3;

    /// <summary>Builds clean or violating source for the modern-syntax preference analyzer.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => GenerateCore(members, violating, shape: null);

    /// <summary>Builds clean or violating source for one modern-syntax preference shape.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <param name="shape">The repeated shape.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating, ModernSyntaxPreferenceBenchmarkShape shape)
        => GenerateCore(members, violating, shape);

    /// <summary>Builds source containing one repeated shape for code-fix benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="shape">The repeated shape.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCodeFix(int members, ModernSyntaxPreferenceBenchmarkShape shape)
        => GenerateCore(members, violating: true, shape);

    /// <summary>Builds the benchmark source body.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <param name="shape">The fixed shape, or <see langword="null"/> to cycle all shapes.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateCore(int members, bool violating, ModernSyntaxPreferenceBenchmarkShape? shape)
        => $$"""
           using System;

           namespace Bench;

           internal sealed class ModernSyntaxPreferenceBench
           {
               private static int Select(Func<int, int> selector, int value) => selector(value);

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating, shape ?? (ModernSyntaxPreferenceBenchmarkShape)(i % ShapeCount)))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating, ModernSyntaxPreferenceBenchmarkShape shape)
        => (shape, violating) switch
        {
            (ModernSyntaxPreferenceBenchmarkShape.Lambda, true) => $$"""
                                                                     private readonly Func<int, int, int> _sum{{index}} = (int left, int right) => left + right + {{index}};
                                                                     """,
            (ModernSyntaxPreferenceBenchmarkShape.Lambda, false) => $$"""
                                                                      private readonly Func<int, int, int> _sum{{index}} = (left, right) => left + right + {{index}};
                                                                      """,
            (ModernSyntaxPreferenceBenchmarkShape.InvocationLambda, true) => $$"""
                                                                               public int Invoke{{index}}() => Select((int value) => value + {{index}}, {{index}});
                                                                               """,
            (ModernSyntaxPreferenceBenchmarkShape.InvocationLambda, false) => $$"""
                                                                                public int Invoke{{index}}() => Select(value => value + {{index}}, {{index}});
                                                                                """,
            (ModernSyntaxPreferenceBenchmarkShape.Accessor, true) => $$"""
                                                                       private int _value{{index}};

                                                                       public int Value{{index}}
                                                                       {
                                                                       {{GenerateAccessor(index, expressionBodied: false)}}
                                                                       }
                                                                       """,
            _ => $$"""
                   private int _value{{index}};

                   public int Value{{index}}
                   {
                   {{GenerateAccessor(index, expressionBodied: true)}}
                   }
                   """
        };

    /// <summary>Builds one accessor, alternating read and write shapes so one member represents one candidate node.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="expressionBodied">Whether to emit the clean expression-bodied form.</param>
    /// <returns>The generated accessor text.</returns>
    private static string GenerateAccessor(int index, bool expressionBodied)
    {
        var writeOnly = (index & 1) == 1;
        return (writeOnly, expressionBodied) switch
        {
            (true, true) => $"        set => _value{index} = value;",
            (true, false) => $"        set {{ _value{index} = value; }}",
            (false, true) => $"        get => _value{index};",
            _ => $"        get {{ return _value{index}; }}"
        };
    }
}
