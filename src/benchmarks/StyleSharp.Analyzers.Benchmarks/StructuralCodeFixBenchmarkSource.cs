// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the structural/layout code-fix benchmark family.</summary>
internal static class StructuralCodeFixBenchmarkSource
{
    /// <summary>The number of directive shapes in the repeated using-sort source pattern.</summary>
    private const int UsingSortDirectivePatternSize = 3;

    /// <summary>Builds violating accessor-order benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateAccessorOrder(int members)
        => $$"""
           namespace Bench;

           internal sealed class AccessorOrderBench
           {
               private int _value;

           {{BenchmarkSourceText.JoinBlocks(members, GenerateAccessorOrderProperty)}}
           }
           """;

    /// <summary>Builds violating accessor-consistency benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateAccessorConsistency(int members)
        => $$"""
           namespace Bench;

           internal sealed class AccessorConsistencyBench
           {
               private int _value;

           {{BenchmarkSourceText.JoinBlocks(members, GenerateAccessorConsistencyProperty)}}
           }
           """;

    /// <summary>Builds violating conditional-on-new-line benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConditionalOnNewLine(int members)
        => $$"""
           namespace Bench;

           internal sealed class ConditionalOnNewLineBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateConditionalOnNewLineMember)}}
           }
           """;

    /// <summary>Builds violating consistent-braces benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConsistentBraces(int members)
        => $$"""
           namespace Bench;

           internal sealed class ConsistentBracesBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateConsistentBracesMember)}}
           }
           """;

    /// <summary>Builds violating empty-statement benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateEmptyStatement(int members)
        => $$"""
           namespace Bench;

           internal sealed class EmptyStatementBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateEmptyStatementMember)}}
           }
           """;

    /// <summary>Builds violating no-public-on-internal-type benchmark source.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateNoPublicOnInternalType(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateNoPublicOnInternalTypeType)}}
           """;

    /// <summary>Builds violating record-init-only benchmark source.</summary>
    /// <param name="types">The number of synthetic record types to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRecordInitOnly(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateRecordInitOnlyType)}}
           """;

    /// <summary>Builds violating record-readonly benchmark source.</summary>
    /// <param name="types">The number of synthetic record structs to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRecordReadonly(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateRecordReadonlyType)}}
           """;

    /// <summary>Builds violating redundant-modifier benchmark source.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRemoveModifier(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateRemoveModifierType)}}
           """;

    /// <summary>Builds violating require-braces benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRequireBraces(int members)
        => $$"""
           namespace Bench;

           internal sealed class RequireBracesBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateRequireBracesMember)}}
           }
           """;

    /// <summary>Builds violating single-line-block-reflow benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateSingleLineBlockReflow(int members)
        => $$"""
           namespace Bench;

           internal sealed class SingleLineBlockReflowBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateSingleLineBlockReflowMember)}}
           }
           """;

    /// <summary>Builds violating multi-line-child-brace benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateMultiLineChildBrace(int members)
        => $$"""
           namespace Bench;

           internal sealed class MultiLineChildBraceBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateMultiLineChildBraceMember)}}
           }
           """;

    /// <summary>Builds violating trailing-comma benchmark source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateTrailingComma(int members)
        => $$"""
           namespace Bench;

           internal sealed class TrailingCommaBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateTrailingCommaMember)}}
           }
           """;

    /// <summary>Builds violating using-directive-qualified benchmark source.</summary>
    /// <param name="types">The number of synthetic namespace blocks to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUsingDirectiveQualified(int types)
        => $$"""
           {{BenchmarkSourceText.JoinBlocks(types, GenerateUsingDirectiveQualifiedNamespace)}}
           """;

    /// <summary>Builds violating using-sort benchmark source.</summary>
    /// <param name="members">The number of synthetic using directives to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUsingSort(int members)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < members; i++)
        {
            AppendUsingSortDirective(builder, i);
        }

        builder
            .AppendLine()
            .AppendLine("namespace Bench;")
            .AppendLine()
            .AppendLine("internal sealed class UsingSortBench")
            .AppendLine("{")
            .AppendLine("}");
        return builder.ToString();
    }

    /// <summary>Builds a violating accessor-order property.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateAccessorOrderProperty(int index)
        => $$"""
           public int Value{{index}}
           {
               set => _value = value;
               get => _value;
           }
           """;

    /// <summary>Builds a violating accessor-consistency property.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateAccessorConsistencyProperty(int index)
        => $$"""
           public int Value{{index}}
           {
               get { return _value; }
               set
               {
                   _value = value;
               }
           }
           """;

    /// <summary>Builds a violating conditional-on-new-line member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateConditionalOnNewLineMember(int index)
        => $$"""
           internal void M{{index}}(bool a, bool b)
           {
               if (a) { } if (b) { }
           }
           """;

    /// <summary>Builds a violating consistent-braces member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateConsistentBracesMember(int index)
        => $$"""
           internal void M{{index}}(bool x)
           {
               if (x)
               {
                   System.Console.WriteLine({{index}});
               }
               else
                   System.Console.WriteLine(-{{index}});
           }
           """;

    /// <summary>Builds a violating empty-statement member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateEmptyStatementMember(int index)
        => $$"""
           internal int M{{index}}()
           {
               var value = {{index}};
               ;
               return value;
           }
           """;

    /// <summary>Builds a violating no-public-on-internal-type declaration.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateNoPublicOnInternalTypeType(int index)
        => $$"""
           internal sealed class C{{index}}
           {
               public void M()
               {
               }
           }
           """;

    /// <summary>Builds a violating record init-only declaration.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateRecordInitOnlyType(int index)
        => $$"""
           public sealed record Person{{index}}
           {
               public string Name { get; set; } = "Person{{index}}";
           }
           """;

    /// <summary>Builds a violating record-readonly declaration.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateRecordReadonlyType(int index)
        => $$"""
           public record struct Point{{index}}(int X, int Y);
           """;

    /// <summary>Builds a violating redundant-modifier declaration.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateRemoveModifierType(int index)
        => $$"""
           public partial class C{{index}}
           {
           }
           """;

    /// <summary>Builds a violating require-braces member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateRequireBracesMember(int index)
        => $$"""
           internal int M{{index}}(bool x)
           {
               if (x) return {{index}};
               return -{{index}};
           }
           """;

    /// <summary>Builds a violating single-line-block-reflow member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateSingleLineBlockReflowMember(int index)
        => $$"""
           internal void M{{index}}() { System.Console.WriteLine({{index}}); }
           """;

    /// <summary>Builds a violating multi-line-child-brace member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateMultiLineChildBraceMember(int index)
        => $$"""
           internal void M{{index}}(bool x)
           {
               if (x)
                   System.Console
                       .WriteLine({{index}});
           }
           """;

    /// <summary>Builds a violating trailing-comma member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateTrailingCommaMember(int index)
        => $$"""
           private static readonly int[] Values{{index}} = new[]
           {
               {{index}},
               {{index + 1}}
           };
           """;

    /// <summary>Builds a violating using-directive-qualified namespace block.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateUsingDirectiveQualifiedNamespace(int index)
        => $$"""
           namespace System.Threading
           {
               using Tasks;

               internal sealed class C{{index}}
               {
                   private Task M() => Task.CompletedTask;
               }
           }
           """;

    /// <summary>Appends one violating using-sort directive.</summary>
    /// <param name="builder">The destination source builder.</param>
    /// <param name="index">The synthetic directive index.</param>
    private static void AppendUsingSortDirective(System.Text.StringBuilder builder, int index)
    {
        switch (index % UsingSortDirectivePatternSize)
        {
            case 0:
            {
                builder.AppendLine("using System.Text;");
                break;
            }

            case 1:
            {
                builder.AppendLine("using System.Collections;");
                break;
            }

            default:
            {
                builder
                    .Append("using X")
                    .Append(index / UsingSortDirectivePatternSize)
                    .AppendLine(" = System.Console;");
                break;
            }
        }
    }
}
