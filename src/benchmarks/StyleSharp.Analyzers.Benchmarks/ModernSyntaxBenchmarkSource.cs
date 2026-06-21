// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the modern-C# analyzer benchmarks (collection expressions,
/// pattern, precedence, and extension-block rules).</summary>
internal static class ModernSyntaxBenchmarkSource
{
    /// <summary>Builds source for empty-collection-expression analysis (SST2100).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit empty collection creations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateEmptyCollectionExpression(int members, bool violating)
        => $$"""
           using System.Collections.Generic;

           namespace Bench;

           internal sealed class EmptyCollectionExpressionBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateEmptyCollectionExpressionMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for explicit-collection-expression analysis (SST2101).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit explicit array initializers.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateExplicitCollectionExpression(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class ExplicitCollectionExpressionBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateExplicitCollectionExpressionMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for nested-ternary analysis (SST1147).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit a conditional nested in a conditional.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateNestedTernary(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class NestedTernaryBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateNestedTernaryMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for null-coalescing-precedence analysis (SST1418).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit an un-parenthesized binary operand of <c>??</c>.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateNullCoalescingPrecedence(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class NullCoalescingPrecedenceBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateNullCoalescingPrecedenceMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for partial-element-access analysis (SST1205).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit partial types without an access modifier.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePartialElementAccess(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class PartialElementAccessBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GeneratePartialElementAccessMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for prefer-extension-block analysis (SST1703).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit classic this-parameter extension methods.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePreferExtensionBlock(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class PreferExtensionBlockBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GeneratePreferExtensionBlockMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for prefer-field-keyword analysis (SST2200).</summary>
    /// <param name="members">The number of synthetic single-property types to emit.</param>
    /// <param name="violating">Whether to emit single-use backing fields with accessor logic.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePreferFieldKeyword(int members, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(members, i => GeneratePreferFieldKeywordType(i, violating))}}
           """;

    /// <summary>Builds source for prefer-switch-expression analysis (SST2201).</summary>
    /// <param name="members">The number of synthetic switch methods to emit.</param>
    /// <param name="violating">Whether to emit return-only switch statements.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePreferSwitchExpression(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class PreferSwitchExpressionBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GeneratePreferSwitchExpressionMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for prefer-or-pattern analysis (SST1144).</summary>
    /// <param name="members">The number of synthetic switch methods to emit.</param>
    /// <param name="violating">Whether to emit stacked combinable case labels.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePreferOrPattern(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class PreferOrPatternBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GeneratePreferOrPatternMethod(i, violating))}}
           }
           """;

    /// <summary>Builds source for query-clause layout analysis (SST1102).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit a blank line between query clauses.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateQueryClause(int members, bool violating)
        => $$"""
           using System.Collections.Generic;
           using System.Linq;

           namespace Bench;

           internal sealed class QueryClauseBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateQueryClauseMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for redundant-parentheses analysis (SST1410).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit anonymous methods with an empty parameter list.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRedundantParentheses(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class RedundantParenthesesBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateRedundantParenthesesMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for use-lambda-syntax analysis (SST1130).</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit anonymous methods.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUseLambdaSyntax(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class UseLambdaSyntaxBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateUseLambdaSyntaxMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member for empty-collection-expression analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit an empty collection creation.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateEmptyCollectionExpressionMember(int index, bool violating)
        => violating
            ? $$"""
               public List<int> Value{{index}} = new List<int>();
               """
            : $$"""
               public List<int> Value{{index}} = new List<int>({{index}} + 1);
               """;

    /// <summary>Builds one clean or violating member for explicit-collection-expression analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit an explicit array initializer.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateExplicitCollectionExpressionMember(int index, bool violating)
        => violating
            ? $$"""
               public int[] Value{{index}} = new[] { {{index}}, {{index + 1}} };
               """
            : $$"""
               public int[] Value{{index}} = [{{index}}, {{index + 1}}];
               """;

    /// <summary>Builds one clean or violating member for nested-ternary analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a nested conditional.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateNestedTernaryMember(int index, bool violating)
        => violating
            ? $$"""
               public int M{{index}}(int x) => x > {{index}} ? (x > 0 ? 1 : 2) : 3;
               """
            : $$"""
               public int M{{index}}(int x) => x > {{index}} ? 1 : 2;
               """;

    /// <summary>Builds one clean or violating member for null-coalescing-precedence analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit an un-parenthesized binary operand of <c>??</c>.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateNullCoalescingPrecedenceMember(int index, bool violating)
        => violating
            ? $$"""
               public int M{{index}}(int? a, int b) => a + b ?? {{index}};
               """
            : $$"""
               public int M{{index}}(int? a, int b) => (a + b) ?? {{index}};
               """;

    /// <summary>Builds one clean or violating nested partial type for partial-element-access analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to omit the access modifier.</param>
    /// <returns>The generated nested type block.</returns>
    private static string GeneratePartialElementAccessMember(int index, bool violating)
        => violating
            ? $$"""
               partial class Part{{index}}
               {
               }
               """
            : $$"""
               private partial class Part{{index}}
               {
               }
               """;

    /// <summary>Builds one clean or violating member for prefer-extension-block analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a classic this-parameter extension method.</param>
    /// <returns>The generated member block.</returns>
    private static string GeneratePreferExtensionBlockMember(int index, bool violating)
        => violating
            ? $$"""
               public static bool IsBlank{{index}}(this string text) => text.Length == {{index}};
               """
            : $$"""
               public static bool IsBlank{{index}}(string text) => text.Length == {{index}};
               """;

    /// <summary>Builds one clean or violating single-property type for prefer-field-keyword analysis.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a non-trivial single-use backing field.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Each property lives in its own type so the analyzer's single-use-backing-field check (which scans the
    /// whole containing type) stays bounded per property — keeping the benchmark a linear measure of
    /// per-property cost rather than the O(n²) a single thousand-property class would produce.
    /// </remarks>
    private static string GeneratePreferFieldKeywordType(int index, bool violating)
        => violating
            ? $$"""
               internal sealed class FieldKeywordBench{{index}}
               {
                   private int _value{{index}};

                   public int Value{{index}}
                   {
                       get => _value{{index}};
                       set => _value{{index}} = value < 0 ? 0 : value;
                   }
               }
               """
            : $$"""
               internal sealed class FieldKeywordBench{{index}}
               {
                   private int _value{{index}};

                   public int Value{{index}}
                   {
                       get => _value{{index}};
                       set => _value{{index}} = value;
                   }
               }
               """;

    /// <summary>Builds one clean or violating switch method for switch-expression analysis.</summary>
    /// <param name="index">The synthetic method index.</param>
    /// <param name="violating">Whether to emit a return-only switch statement.</param>
    /// <returns>The generated method block.</returns>
    private static string GeneratePreferSwitchExpressionMember(int index, bool violating)
        => violating
            ? $$"""
               public int M{{index}}(int value)
               {
                   switch (value)
                   {
                       case 0:
                           return {{index}};
                       case 1:
                           return {{index + 1}};
                       default:
                           return -1;
                   }
               }
               """
            : $$"""
               public int M{{index}}(int value) => value switch
               {
                   0 => {{index}},
                   1 => {{index + 1}},
                   _ => -1,
               };
               """;

    /// <summary>Builds one clean or violating switch method for prefer-or-pattern analysis.</summary>
    /// <param name="index">The synthetic method index.</param>
    /// <param name="violating">Whether to emit stacked combinable case labels.</param>
    /// <returns>The generated method block.</returns>
    /// <remarks>
    /// Each method holds its own small switch so the corpus scales linearly; a single thousand-section
    /// switch instead makes Roslyn's decision-DAG binding cost dominate and is not what this rule measures.
    /// </remarks>
    private static string GeneratePreferOrPatternMethod(int index, bool violating)
        => violating
            ? $$"""
               public int Classify{{index}}(int value)
               {
                   switch (value)
                   {
                       case 0:
                       case 1:
                           return 0;
                       case 2:
                       case 3:
                           return 1;
                       default:
                           return -1;
                   }
               }
               """
            : $$"""
               public int Classify{{index}}(int value)
               {
                   switch (value)
                   {
                       case 0:
                           return 0;
                       case 1:
                           return 1;
                       default:
                           return -1;
                   }
               }
               """;

    /// <summary>Builds one clean or violating query member for query-clause layout analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a blank line between clauses.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateQueryClauseMember(int index, bool violating)
        => violating
            ? $$"""
               public IEnumerable<int> Q{{index}}(int[] source) =>
                   from x in source
                   where x > {{index}}

                   select x;
               """
            : $$"""
               public IEnumerable<int> Q{{index}}(int[] source) =>
                   from x in source
                   where x > {{index}}
                   select x;
               """;

    /// <summary>Builds one clean or violating member for redundant-parentheses analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit an anonymous method with an empty parameter list.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateRedundantParenthesesMember(int index, bool violating)
        => violating
            ? $$"""
               public System.Action Value{{index}} = delegate() { };
               """
            : $$"""
               public System.Action Value{{index}} = delegate { };
               """;

    /// <summary>Builds one clean or violating member for use-lambda-syntax analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit an anonymous method.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateUseLambdaSyntaxMember(int index, bool violating)
        => violating
            ? $$"""
               public System.Action Value{{index}} = delegate { };
               """
            : $$"""
               public System.Action Value{{index}} = () => { };
               """;
}
