// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the discrete per-analyzer benchmark family.</summary>
internal static class DiscreteAnalyzerBenchmarkSource
{
    /// <summary>Builds source for multiple-statements-on-line analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit same-line statement violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateMultipleStatementsOnLine(int members, bool violating)
        => GenerateStaticClass(
            "MultipleStatementsOnLineBench",
            members,
            violating,
            static (index, isViolating) => isViolating ? GenerateMultipleStatementsOnLineViolatingMember(index) : GenerateMultipleStatementsOnLineCleanMember(index));

    /// <summary>Builds source for conditional-operator-placement analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit trailing wrapped operators.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConditionalOperatorPlacement(int members, bool violating)
        => GenerateStaticClass(
            "ConditionalOperatorPlacementBench",
            members,
            violating,
            static (index, isViolating) => isViolating ? GenerateConditionalOperatorPlacementViolatingMember(index) : GenerateConditionalOperatorPlacementCleanMember(index));

    /// <summary>Builds source for trailing-comma analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to omit the final trailing comma.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateTrailingComma(int members, bool violating)
        => GenerateStaticClass(
            "TrailingCommaBench",
            members,
            violating,
            static (index, isViolating) => isViolating ? GenerateTrailingCommaViolatingMember(index) : GenerateTrailingCommaCleanMember(index));

    /// <summary>Builds source for single-line-element analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to collapse element bodies onto one line.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateSingleLineElement(int members, bool violating)
        => GenerateStaticClass(
            "SingleLineElementBench",
            members,
            violating,
            static (index, isViolating) => isViolating ? GenerateSingleLineElementViolatingMember(index) : GenerateSingleLineElementCleanMember(index));

    /// <summary>Builds source for readable-conditions analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit yoda comparisons.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUseReadableConditions(int members, bool violating)
        => GenerateStaticClass(
            "UseReadableConditionsBench",
            members,
            violating,
            static (index, isViolating) => isViolating ? GenerateUseReadableConditionsViolatingMember(index) : GenerateUseReadableConditionsCleanMember(index));

    /// <summary>Builds a static class wrapping the requested synthetic members.</summary>
    /// <param name="className">The generated class name.</param>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit violating members.</param>
    /// <param name="memberFactory">Builds one clean or violating member.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateStaticClass(string className, int members, bool violating, Func<int, bool, string> memberFactory)
        => $$"""
           namespace Bench;

           internal static class {{className}}
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => memberFactory(i, violating))}}
           }
           """;

    /// <summary>Builds one clean multiple-statements-on-line member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMultipleStatementsOnLineCleanMember(int index)
        => $$"""
           internal static void M{{index}}()
           {
               System.Console.WriteLine({{index}});
               System.Console.WriteLine({{index + 1}});
           }
           """;

    /// <summary>Builds one violating multiple-statements-on-line member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMultipleStatementsOnLineViolatingMember(int index)
        => $$"""
           internal static void M{{index}}()
           {
               System.Console.WriteLine({{index}}); System.Console.WriteLine({{index + 1}});
           }
           """;

    /// <summary>Builds one clean conditional-operator-placement member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateConditionalOperatorPlacementCleanMember(int index)
        => $$"""
           internal static int M{{index}}(bool condition) => condition
               ? {{index}}
               : {{index + 1}};
           """;

    /// <summary>Builds one violating conditional-operator-placement member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateConditionalOperatorPlacementViolatingMember(int index)
        => $$"""
           internal static int M{{index}}(bool condition) => condition ?
               {{index}} :
               {{index + 1}};
           """;

    /// <summary>Builds one clean trailing-comma member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateTrailingCommaCleanMember(int index)
        => $$"""
           private static readonly int[] Values{{index}} = new[]
           {
               {{index}},
               {{index + 1}},
           };
           """;

    /// <summary>Builds one violating trailing-comma member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateTrailingCommaViolatingMember(int index)
        => $$"""
           private static readonly int[] Values{{index}} = new[]
           {
               {{index}},
               {{index + 1}}
           };
           """;

    /// <summary>Builds one clean single-line-element member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateSingleLineElementCleanMember(int index)
        => $$"""
           internal static void M{{index}}()
           {
               System.Console.WriteLine({{index}});
           }
           """;

    /// <summary>Builds one violating single-line-element member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateSingleLineElementViolatingMember(int index)
        => $$"""
           internal static void M{{index}}() { System.Console.WriteLine({{index}}); }
           """;

    /// <summary>Builds one clean readable-conditions member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateUseReadableConditionsCleanMember(int index)
        => $$"""
           internal static bool M{{index}}(int count) => count == {{index}};
           """;

    /// <summary>Builds one violating readable-conditions member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateUseReadableConditionsViolatingMember(int index)
        => $$"""
           internal static bool M{{index}}(int count) => {{index}} == count;
           """;
}
