// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for modern-syntax value analysis.</summary>
internal static class ModernSyntaxValueBenchmarkSource
{
    /// <summary>The number of shapes cycled by the analyzer benchmark corpus.</summary>
    private const int ShapeCount = 14;

    /// <summary>Builds clean or violating source for the modern-syntax value analyzer.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => GenerateCore(members, violating, shape: null);

    /// <summary>Builds source containing one repeated shape for code-fix benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="shape">The repeated shape.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCodeFix(int members, ModernSyntaxValueBenchmarkShape shape)
        => GenerateCore(members, violating: true, shape);

    /// <summary>Builds the benchmark source body.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <param name="shape">The fixed shape, or <see langword="null"/> to cycle all shapes.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateCore(int members, bool violating, ModernSyntaxValueBenchmarkShape? shape)
        => $$"""
           using System;
           using System.Linq;

           namespace Bench;

           internal class Base
           {
           }

           internal sealed class Derived : Base
           {
           }

           internal sealed class Castable
           {
               public static explicit operator Base(Castable value) => new Derived();
           }

           internal sealed class ModernSyntaxValueBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating, shape ?? (ModernSyntaxValueBenchmarkShape)(i % ShapeCount)))}}

               private int Compute(int value) => value + 1;
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating, ModernSyntaxValueBenchmarkShape shape)
        => GenerateOriginalMember(index, violating, shape) ?? GenerateAdditionalMember(index, violating, shape);

    /// <summary>Builds one synthetic member from the original value-shape batch.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The generated member block, or <see langword="null"/>.</returns>
    private static string? GenerateOriginalMember(int index, bool violating, ModernSyntaxValueBenchmarkShape shape)
        => shape switch
        {
            ModernSyntaxValueBenchmarkShape.Interpolation => GenerateInterpolation(index, violating),
            ModernSyntaxValueBenchmarkShape.IgnoredValue => GenerateIgnoredValue(index, violating),
            ModernSyntaxValueBenchmarkShape.OverwrittenValue => GenerateOverwrittenValue(index, violating),
            ModernSyntaxValueBenchmarkShape.CoalesceAssignment => GenerateCoalesceAssignment(index, violating),
            ModernSyntaxValueBenchmarkShape.AnonymousTuple => GenerateAnonymousTuple(index, violating),
            ModernSyntaxValueBenchmarkShape.ForeachCast => GenerateForeachCast(index, violating),
            ModernSyntaxValueBenchmarkShape.HiddenCast => GenerateHiddenCast(index, violating),
            ModernSyntaxValueBenchmarkShape.FoldNullCheck => GenerateFoldNullCheck(index, violating),
            _ => null
        };

    /// <summary>Builds one synthetic member from the additional value-shape batch.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateAdditionalMember(int index, bool violating, ModernSyntaxValueBenchmarkShape shape)
        => shape switch
        {
            ModernSyntaxValueBenchmarkShape.LocalFunction => GenerateLocalFunction(index, violating),
            ModernSyntaxValueBenchmarkShape.WhereTerminal => GenerateWhereTerminal(index, violating),
            ModernSyntaxValueBenchmarkShape.TypeFilter => GenerateTypeFilter(index, violating),
            ModernSyntaxValueBenchmarkShape.NullPattern => GenerateNullPattern(index, violating),
            ModernSyntaxValueBenchmarkShape.UnboundGenericName => GenerateUnboundGenericName(index, violating),
            _ => GenerateHotPathLinq(index, violating)
        };

    /// <summary>Builds one interpolation shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateInterpolation(int index, bool violating)
        => violating
            ? $$"""
                private string Interpolation{{index}}(int value) => $"Value: {value.ToString("X")}";
                """
            : $$"""
                private string Interpolation{{index}}(int value) => $"Value: {value:X}";
                """;

    /// <summary>Builds one ignored-value shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateIgnoredValue(int index, bool violating)
        => violating
            ? $$"""
                private void IgnoredValue{{index}}()
                {
                    Compute({{index}});
                }
                """
            : $$"""
                private void IgnoredValue{{index}}()
                {
                    _ = Compute({{index}});
                }
                """;

    /// <summary>Builds one overwritten-value shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateOverwrittenValue(int index, bool violating)
        => violating
            ? $$"""
                private int OverwrittenValue{{index}}()
                {
                    int value = 0;
                    value = {{index}};
                    return value;
                }
                """
            : $$"""
                private int OverwrittenValue{{index}}()
                {
                    int value;
                    value = {{index}};
                    return value;
                }
                """;

    /// <summary>Builds one coalescing-assignment shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCoalesceAssignment(int index, bool violating)
        => violating
            ? $$"""
                private string CoalesceAssignment{{index}}()
                {
                    string value = null;
                    if (value is null)
                    {
                        value = "{{index}}";
                    }

                    return value;
                }
                """
            : $$"""
                private string CoalesceAssignment{{index}}()
                {
                    string value = null;
                    value ??= "{{index}}";
                    return value;
                }
                """;

    /// <summary>Builds one anonymous tuple shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateAnonymousTuple(int index, bool violating)
        => violating
            ? $$"""
                private object AnonymousTuple{{index}}(int id, string name) => new { id, Label = name };
                """
            : $$"""
                private object AnonymousTuple{{index}}(int id, string name) => (id, Label: name);
                """;

    /// <summary>Builds one foreach-cast shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateForeachCast(int index, bool violating)
        => violating
            ? $$"""
                private void ForeachCast{{index}}(object[] values)
                {
                    foreach (string value in values)
                    {
                    }
                }
                """
            : $$"""
                private void ForeachCast{{index}}(object[] values)
                {
                    foreach (string value in System.Linq.Enumerable.Cast<string>(values))
                    {
                    }
                }
                """;

    /// <summary>Builds one hidden-cast shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateHiddenCast(int index, bool violating)
        => violating
            ? $$"""
                private Derived HiddenCast{{index}}(Castable value) => (Derived)value;
                """
            : $$"""
                private Derived HiddenCast{{index}}(Castable value) => (Derived)(Base)value;
                """;

    /// <summary>Builds one null-check fold shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateFoldNullCheck(int index, bool violating)
        => violating
            ? $$"""
                private string FoldNullCheck{{index}}(string input)
                {
                    string value = input;
                    if (value == null)
                    {
                        throw new InvalidOperationException();
                    }

                    return value;
                }
                """
            : $$"""
                private string FoldNullCheck{{index}}(string input)
                {
                    string value = input ?? throw new InvalidOperationException();
                    return value;
                }
                """;

    /// <summary>Builds one local-function shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateLocalFunction(int index, bool violating)
        => violating
            ? $$"""
                private int LocalFunction{{index}}()
                {
                    Func<int, int> add = value => value + {{index}};
                    return add(1);
                }
                """
            : $$"""
                private int LocalFunction{{index}}()
                {
                    int add(int value) => value + {{index}};
                    return add(1);
                }
                """;

    /// <summary>Builds one LINQ Where-terminal shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateWhereTerminal(int index, bool violating)
        => violating
            ? $$"""
                private bool WhereTerminal{{index}}(int[] values) => values.Where(value => value > {{index}}).Any();
                """
            : $$"""
                private bool WhereTerminal{{index}}(int[] values) => values.Any(value => value > {{index}});
                """;

    /// <summary>Builds one LINQ type-filter shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateTypeFilter(int index, bool violating)
        => violating
            ? $$"""
                private object TypeFilter{{index}}(object[] values) => values.Where(value => value is string).Cast<string>();
                """
            : $$"""
                private object TypeFilter{{index}}(object[] values) => values.OfType<string>();
                """;

    /// <summary>Builds one direct null-pattern shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateNullPattern(int index, bool violating)
        => violating
            ? $$"""
                private bool NullPattern{{index}}(object value) => value is object;
                """
            : $$"""
                private bool NullPattern{{index}}(object value) => value is not null;
                """;

    /// <summary>Builds one generic nameof shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateUnboundGenericName(int index, bool violating)
        => violating
            ? $$"""
                private string UnboundGenericName{{index}}() => nameof(System.Collections.Generic.Dictionary<string, int>);
                """
            : $$"""
                private string UnboundGenericName{{index}}() => nameof(System.Collections.Generic.Dictionary<,>);
                """;

    /// <summary>Builds one hot-path LINQ shape.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit the reportable form.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateHotPathLinq(int index, bool violating)
        => violating
            ? $$"""
                private object HotPathLinq{{index}}(int[] values) => values.Select(value => value + {{index}});
                """
            : $$"""
                private int HotPathLinq{{index}}(int[] values)
                {
                    var sum = 0;
                    for (var i = 0; i < values.Length; i++)
                    {
                        sum += values[i] + {{index}};
                    }

                    return sum;
                }
                """;
}
