// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for documentation-oriented code-fix benchmarks.</summary>
internal static class DocumentationCodeFixBenchmarkSource
{
    /// <summary>Builds headerless source for file-header code-fix benchmarks.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateFileHeader(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateHeaderlessType)}}
           """;

    /// <summary>Builds documentation text missing terminal periods.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateDocumentationPeriod(int members)
        => $$"""
           namespace Bench;

           internal sealed class DocumentationPeriodCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateDocumentationPeriodMethod)}}
           }
           """;

    /// <summary>Builds property summaries missing the accessor prefix.</summary>
    /// <param name="members">The number of synthetic properties to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePropertySummary(int members)
        => $$"""
           namespace Bench;

           internal sealed class PropertySummaryCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GeneratePropertySummaryProperty)}}
           }
           """;

    /// <summary>Builds restricted-setter property summaries that still say <c>Gets or sets</c>.</summary>
    /// <param name="members">The number of synthetic properties to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRestrictedPropertySummary(int members)
        => $$"""
           namespace Bench;

           internal sealed class RestrictedPropertySummaryCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateRestrictedPropertySummaryProperty)}}
           }
           """;

    /// <summary>Builds constructor summaries that need the standard wording.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConstructorSummary(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateConstructorType)}}
           """;

    /// <summary>Builds destructor summaries that need the standard wording.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateDestructorSummary(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateDestructorType)}}
           """;

    /// <summary>Builds undocumented-parameter source for documentation-stub benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateDocumentationStub(int members)
        => $$"""
           namespace Bench;

           internal sealed class DocumentationStubCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateDocumentationStubMethod)}}
           }
           """;

    /// <summary>Builds one type without a file header.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateHeaderlessType(int index)
        => $$"""
           internal sealed class C{{index}}
           {
           }
           """;

    /// <summary>Builds one documented member missing its terminal period.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateDocumentationPeriodMethod(int index)
        => $$"""
           /// <summary>Does work {{index}}</summary>
           internal void M{{index}}()
           {
           }
           """;

    /// <summary>Builds one property summary lacking the accessor prefix.</summary>
    /// <param name="index">The synthetic property index.</param>
    /// <returns>The generated property block.</returns>
    private static string GeneratePropertySummaryProperty(int index)
        => $$"""
           /// <summary>The value {{index}}.</summary>
           public int Value{{index}} { get; set; }
           """;

    /// <summary>Builds one restricted-setter property summary using the wrong phrase.</summary>
    /// <param name="index">The synthetic property index.</param>
    /// <returns>The generated property block.</returns>
    private static string GenerateRestrictedPropertySummaryProperty(int index)
        => $$"""
           /// <summary>Gets or sets the value {{index}}.</summary>
           public int Value{{index}} { get; private set; }
           """;

    /// <summary>Builds one type whose constructor summary needs standardization.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateConstructorType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               /// <summary>Creates the thing.</summary>
               public C{{index}}()
               {
               }
           }
           """;

    /// <summary>Builds one type whose destructor summary needs standardization.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateDestructorType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               /// <summary>Cleans up.</summary>
               ~C{{index}}()
               {
               }
           }
           """;

    /// <summary>Builds one method with an undocumented parameter.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateDocumentationStubMethod(int index)
        => $$"""
           /// <summary>Does a thing.</summary>
           public void M{{index}}(int value{{index}})
           {
           }
           """;
}
