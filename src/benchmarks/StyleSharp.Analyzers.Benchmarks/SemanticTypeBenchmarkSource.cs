// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for semantic and type-oriented analyzer benchmarks.</summary>
internal static class SemanticTypeBenchmarkSource
{
    /// <summary>Builds source for trivial-auto-property analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit trivial property wrappers.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateTrivialAutoProperty(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class TrivialAutoPropertyBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateTrivialAutoPropertyMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for redundant-modifier analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit single-part partial declarations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRedundantModifier(int members, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateRedundantModifierMember(i, violating))}}
           """;

    /// <summary>Builds source for default-value-type-constructor analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit parameterless struct constructions.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateDefaultValueTypeConstructor(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class DefaultValueTypeConstructorBench
           {
               private struct ValueType
               {
                   public int Value;
               }

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateDefaultValueTypeConstructorMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for use-string-empty analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit empty string literals.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUseStringEmpty(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class UseStringEmptyBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateUseStringEmptyMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for use-nullable-shorthand analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit Nullable&lt;T&gt; spellings.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUseNullableShorthand(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class UseNullableShorthandBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateUseNullableShorthandMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for use-tuple-syntax analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit ValueTuple spellings.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUseTupleSyntax(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class UseTupleSyntaxBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateUseTupleSyntaxMember(i, violating))}}
           }
           """;

    /// <summary>Builds source for do-not-prefix-with-base analyzer benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit redundant base member access.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateDoNotPrefixWithBase(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class DoNotPrefixWithBaseBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateDoNotPrefixWithBaseMember(i, violating))}}
           }
           """;

    private static string GenerateTrivialAutoPropertyMember(int index, bool violating)
    {
        if (!violating)
        {
            return $$"""
                   public int Value{{index}} { get; set; }
                   """;
        }

        return $$"""
               private int _value{{index}};

               public int Value{{index}}
               {
                   get => _value{{index}};
                   set => _value{{index}} = value;
               }
               """;
    }

    private static string GenerateRedundantModifierMember(int index, bool violating)
        => violating
            ? $$"""
               internal partial class C{{index}}
               {
               }
               """
            : $$"""
               internal sealed class C{{index}}
               {
               }
               """;

    private static string GenerateDefaultValueTypeConstructorMember(int index, bool violating)
        => violating
            ? $$"""
               internal static ValueType M{{index}}() => new ValueType();
               """
            : $$"""
               internal static object M{{index}}() => new object();
               """;

    private static string GenerateUseStringEmptyMember(int index, bool violating)
        => violating
            ? $$"""
               internal static string M{{index}}() => "";
               """
            : $$"""
               internal static string M{{index}}() => "value{{index}}";
               """;

    private static string GenerateUseNullableShorthandMember(int index, bool violating)
        => violating
            ? $$"""
               private global::System.Nullable<int> _value{{index}} = {{index}};
               """
            : $$"""
               private global::System.Collections.Generic.List<int> _value{{index}} = [];
               """;

    private static string GenerateUseTupleSyntaxMember(int index, bool violating)
        => violating
            ? $$"""
               private global::System.ValueTuple<int, int> _value{{index}} = ({{index}}, {{index + 1}});
               """
            : $$"""
               private global::System.Collections.Generic.Dictionary<int, int> _value{{index}} = [];
               """;

    private static string GenerateDoNotPrefixWithBaseMember(int index, bool violating)
        => $$"""
           private class Base{{index}}
           {
               protected int Value{{index}} = {{index}};
           }

           private sealed class Derived{{index}} : Base{{index}}
           {
               internal int M() => {{(violating ? $"base.Value{index}" : $"this.Value{index}")}};
           }
           """;
}
