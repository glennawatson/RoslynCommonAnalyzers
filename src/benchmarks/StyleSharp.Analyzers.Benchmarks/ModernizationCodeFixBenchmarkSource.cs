// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for modernization code-fix benchmarks that lack analyzer benchmark corpora.</summary>
internal static class ModernizationCodeFixBenchmarkSource
{
    /// <summary>The number of labels emitted per violating switch section.</summary>
    private const int LabelsPerSection = 2;

    /// <summary>The number of property members emitted into each synthetic type for property code-fix benchmarks.</summary>
    private const int TrivialPropertyMembersPerType = 25;

    /// <summary>The offset used for the third element in explicit collection initializers.</summary>
    private const int ThirdCollectionValueOffset = 2;

    /// <summary>Builds distributed trivial-auto-property source for property-focused code-fix benchmarks.</summary>
    /// <param name="members">The number of violating properties to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateTrivialAutoPropertyCodeFix(int members)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("namespace Bench;").AppendLine();

        var typeIndex = 0;
        var memberIndex = 0;
        while (memberIndex < members)
        {
            builder.Append("internal sealed class TrivialAutoPropertyBench")
                .Append(typeIndex)
                .AppendLine()
                .AppendLine("{");

            var typeMemberCount = Math.Min(TrivialPropertyMembersPerType, members - memberIndex);
            for (var i = 0; i < typeMemberCount; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine().AppendLine();
                }

                AppendTrivialAutoPropertyMember(builder, memberIndex + i);
            }

            builder.AppendLine().AppendLine("}");
            memberIndex += typeMemberCount;
            typeIndex++;
            if (memberIndex < members)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    /// <summary>Builds stacked switch labels for the prefer-or-pattern code-fix benchmark.</summary>
    /// <param name="sections">The number of violating switch sections to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePreferOrPattern(int sections)
        => $$"""
           namespace Bench;

           internal static class PreferOrPatternBench
           {
               internal static int M(int value)
               {
                   switch (value)
                   {
           {{BenchmarkSourceText.JoinBlocks(sections, GeneratePreferOrPatternSection)}}
                       default:
                           return -1;
                   }
               }
           }
           """;

    /// <summary>Builds redundant empty parentheses for anonymous delegates or attributes.</summary>
    /// <param name="members">The number of violating members to emit.</param>
    /// <param name="attribute">Whether to emit empty attribute argument lists instead of delegate parameter lists.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRedundantParentheses(int members, bool attribute)
        => $$"""
           using System;

           namespace Bench;

           internal static class RedundantParenthesesBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => attribute ? GenerateRedundantParenthesesAttributeMember(i) : GenerateRedundantParenthesesDelegateMember(i))}}
           }
           """;

    /// <summary>Builds casted numeric literals for the literal-suffix code-fix benchmark.</summary>
    /// <param name="members">The number of violating members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUseLiteralSuffix(int members)
        => $$"""
           namespace Bench;

           internal static class UseLiteralSuffixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateUseLiteralSuffixMember)}}
           }
           """;

    /// <summary>Builds empty or explicit collection creations for the collection-expression code-fix benchmark.</summary>
    /// <param name="members">The number of violating members to emit.</param>
    /// <param name="explicitCollection">Whether to emit explicit collection initializers instead of empty collections.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCollectionExpression(int members, bool explicitCollection)
        => $$"""
           namespace Bench;

           internal static class CollectionExpressionBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => explicitCollection ? GenerateExplicitCollectionMember(i) : GenerateEmptyCollectionMember(i))}}
           }
           """;

    /// <summary>Builds one switch section containing a stacked-label violation.</summary>
    /// <param name="index">The zero-based switch-section index.</param>
    /// <returns>The generated switch-section source.</returns>
    private static string GeneratePreferOrPatternSection(int index)
        => $$"""
                       case {{index * LabelsPerSection}}:
                       case {{(index * LabelsPerSection) + 1}}:
                           return {{index}};
           """;

    /// <summary>Builds one anonymous-delegate member with redundant empty parentheses.</summary>
    /// <param name="index">The zero-based member index.</param>
    /// <returns>The generated member source.</returns>
    private static string GenerateRedundantParenthesesDelegateMember(int index)
        => $$"""
           internal static Action M{{index}}() => delegate() { };
           """;

    /// <summary>Builds one attributed member with redundant empty attribute parentheses.</summary>
    /// <param name="index">The zero-based member index.</param>
    /// <returns>The generated member source.</returns>
    private static string GenerateRedundantParenthesesAttributeMember(int index)
        => $$"""
           [Obsolete()]
           internal static void M{{index}}()
           {
           }
           """;

    /// <summary>Builds one casted numeric-literal member for the literal-suffix benchmark.</summary>
    /// <param name="index">The zero-based member index.</param>
    /// <returns>The generated member source.</returns>
    private static string GenerateUseLiteralSuffixMember(int index)
        => $$"""
           internal static long M{{index}}() => (long){{index + 1}};
           """;

    /// <summary>Appends one violating backing-field/property pair for the property code-fix benchmarks.</summary>
    /// <param name="builder">The destination source builder.</param>
    /// <param name="index">The zero-based property index.</param>
    private static void AppendTrivialAutoPropertyMember(System.Text.StringBuilder builder, int index)
    {
        builder.Append("    private int _value")
            .Append(index)
            .AppendLine(";")
            .AppendLine()
            .Append("    public int Value")
            .Append(index)
            .AppendLine()
            .AppendLine("    {")
            .Append("        get => _value")
            .Append(index)
            .AppendLine(";")
            .Append("        set => _value")
            .Append(index)
            .AppendLine(" = value;")
            .Append("    }");
    }

    /// <summary>Builds one member that returns an empty collection via a factory call.</summary>
    /// <param name="index">The zero-based member index.</param>
    /// <returns>The generated member source.</returns>
    private static string GenerateEmptyCollectionMember(int index)
        => $$"""
           internal static int[] M{{index}}() => System.Array.Empty<int>();
           """;

    /// <summary>Builds one member that returns an explicit collection initializer.</summary>
    /// <param name="index">The zero-based member index.</param>
    /// <returns>The generated member source.</returns>
    private static string GenerateExplicitCollectionMember(int index)
        => $$"""
           internal static int[] M{{index}}() => new[] { {{index}}, {{index + 1}}, {{index + ThirdCollectionValueOffset}} };
           """;
}
