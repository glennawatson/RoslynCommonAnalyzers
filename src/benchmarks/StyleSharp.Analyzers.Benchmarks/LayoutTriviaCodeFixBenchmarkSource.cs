// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for layout, trivia, and text-oriented code-fix benchmarks.</summary>
internal static class LayoutTriviaCodeFixBenchmarkSource
{
    /// <summary>Builds empty-comment source for comment-content code-fix benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateCommentContent(int members)
        => $$"""
           namespace Bench;

           internal sealed class CommentContentCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateCommentContentMethod)}}
           }
           """;

    /// <summary>Builds multiple-whitespace source for spacing code-fix benchmarks.</summary>
    /// <param name="members">The number of synthetic fields to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateSpacing(int members)
        => $$"""
           namespace Bench;

           internal sealed class SpacingCodeFixBench
           {
           {{BenchmarkSourceText.JoinLines(members, GenerateSpacingField)}}
           }
           """;

    /// <summary>Builds standalone-comment source for single-line-comment-spacing benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateSingleLineCommentSpacing(int members)
        => $$"""
           namespace Bench;

           internal sealed class SingleLineCommentSpacingCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateSingleLineCommentSpacingMethod)}}
           }
           """;

    /// <summary>Builds source with leading blank lines for file-start benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateFileStartBlankLines(int members)
        => "\n\nnamespace Bench;\n\ninternal sealed class FileStartBlankLinesCodeFixBench\n{\n"
            + BenchmarkSourceText.JoinBlocks(members, GenerateSimpleMethod)
            + "\n}";

    /// <summary>Builds source lacking a trailing newline for file-ending benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateFileEnding(int members)
        => "namespace Bench;\n\ninternal sealed class FileEndingCodeFixBench\n{\n"
            + BenchmarkSourceText.JoinBlocks(members, GenerateSimpleMethod)
            + "\n}";

    /// <summary>Builds source with a blank line after opening braces for blank-line-removal benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateBlankLineRemoval(int members)
        => $$"""
           namespace Bench;

           internal sealed class BlankLineRemovalCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateBlankLineRemovalMethod)}}
           }
           """;

    /// <summary>Builds source with repeated extra blank lines for multiple-blank-lines benchmarks.</summary>
    /// <param name="members">The number of synthetic fields to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateMultipleBlankLines(int members)
        => $$"""
           namespace Bench;

           internal sealed class MultipleBlankLinesCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateMultipleBlankLinesField)}}
           }
           """;

    /// <summary>Builds source with blank lines before chained keywords for chained-block-spacing benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateChainedBlockSpacing(int members)
        => $$"""
           namespace Bench;

           internal sealed class ChainedBlockSpacingCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateChainedBlockSpacingMethod)}}
           }
           """;

    /// <summary>Builds source with statements immediately following closing braces for closing-brace-spacing benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateClosingBraceSpacing(int members)
        => $$"""
           namespace Bench;

           internal sealed class ClosingBraceSpacingCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateClosingBraceSpacingMethod)}}
           }
           """;

    /// <summary>Builds source with a blank line after documentation headers for documentation-header-spacing benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateDocumentationHeaderSpacing(int members)
        => $$"""
           namespace Bench;

           internal sealed class DocumentationHeaderSpacingCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateDocumentationHeaderSpacingMethod)}}
           }
           """;

    /// <summary>Builds source with adjacent members for element-spacing benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateElementSpacing(int members)
        => $$"""
           namespace Bench;

           internal sealed class ElementSpacingCodeFixBench
           {
           {{BenchmarkSourceText.JoinLines(members, GenerateElementSpacingMethod)}}
           }
           """;

    /// <summary>Builds one member containing an empty comment.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCommentContentMethod(int index)
        => $$"""
           private static void M{{index}}()
           {
               //
               var value = {{index}};
               _ = value;
           }
           """;

    /// <summary>Builds one field with multiple spaces between the modifier and type.</summary>
    /// <param name="index">The synthetic field index.</param>
    /// <returns>The generated field line.</returns>
    private static string GenerateSpacingField(int index) => "    private  int _value" + index + ";";

    /// <summary>Builds one method with a standalone comment lacking the separating blank line above it.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateSingleLineCommentSpacingMethod(int index)
        => $$"""
           private static void M{{index}}()
           {
               var a = {{index}};
               // comment {{index}}
               var b = a;
               _ = b;
           }
           """;

    /// <summary>Builds one simple method body.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateSimpleMethod(int index)
        => $$"""
           private static void M{{index}}()
           {
               _ = {{index}};
           }
           """;

    /// <summary>Builds one method with a blank line immediately after the opening brace.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateBlankLineRemovalMethod(int index)
        => $$"""
           private static void M{{index}}()
           {

               _ = {{index}};
           }
           """;

    /// <summary>Builds one field terminated with a newline so the joiner produces extra blank lines.</summary>
    /// <param name="index">The synthetic field index.</param>
    /// <returns>The generated field line.</returns>
    private static string GenerateMultipleBlankLinesField(int index)
        => "    private int _value" + index + ";" + Environment.NewLine;

    /// <summary>Builds one method with a blank line before <c>else</c>.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateChainedBlockSpacingMethod(int index)
        => $$"""
           private static void M{{index}}(bool flag)
           {
               if (flag)
               {
               }

               else
               {
                   _ = {{index}};
               }
           }
           """;

    /// <summary>Builds one method with code immediately after a nested closing brace.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateClosingBraceSpacingMethod(int index)
        => $$"""
           private static void M{{index}}(bool flag)
           {
               if (flag)
               {
               }
               _ = {{index}};
           }
           """;

    /// <summary>Builds one documented member with a blank line between the header and declaration.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateDocumentationHeaderSpacingMethod(int index)
        => $$"""
           /// <summary>Does work {{index}}.</summary>

           private static void M{{index}}()
           {
           }
           """;

    /// <summary>Builds one method declaration for adjacent-member spacing benchmarks.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateElementSpacingMethod(int index)
        => $$"""
           private static void M{{index}}()
           {
           }
           """;
}
