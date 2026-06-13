// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves a second-or-later top-level type out to its own file so the file declares a single
/// type (SST1402). The new file keeps the original namespace, using directives and file header
/// (the syntax is copied and the sibling types removed, so formatting is preserved verbatim —
/// no semantic model, formatter or simplifier runs). The new file is created once and linked
/// into every target framework, and its name follows the configured generic convention
/// (<c>Widget{T}.cs</c> by default, <c>Widget`1.cs</c> under <c>metadata</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1402MoveTypeToFileCodeFixProvider))]
[Shared]
public sealed class Sst1402MoveTypeToFileCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.SingleType.Id);

    /// <inheritdoc/>
    /// <remarks>FixAll is disabled: this fix adds a new document, which the batch fixer (text-edit
    /// merging only) cannot carry, so each occurrence is fixed individually.</remarks>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var tree = await context.Document.GetSyntaxTreeAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax || tree is null)
        {
            return;
        }

        var options = context.Document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree);
        var useMetadata = TypeFileNaming.UseMetadataConvention(options, MaintainabilityRules.SingleType.Id);

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<BaseTypeDeclarationSyntax>() is not { } type)
            {
                continue;
            }

            var fileName = TypeFileNaming.Stem(type, useMetadata) + ".cs";
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Move type to '{fileName}'",
                    cancellationToken => MoveAsync(context.Document, type, fileName, cancellationToken),
                    equivalenceKey: nameof(Sst1402MoveTypeToFileCodeFixProvider) + fileName),
                diagnostic);
        }
    }

    /// <summary>Extracts the type into a new document and removes it from the original (and every linked copy).</summary>
    /// <param name="document">The document containing the type.</param>
    /// <param name="type">The type declaration to move.</param>
    /// <param name="fileName">The new file's name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated solution.</returns>
    internal static async Task<Solution> MoveAsync(Document document, BaseTypeDeclarationSyntax type, string fileName, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document.Project.Solution;
        }

        var extractedRoot = BuildExtractedRoot(compilationUnit, type);
        var originalRoot = compilationUnit.RemoveNode(type, SyntaxRemoveOptions.KeepUnbalancedDirectives);
        if (extractedRoot is null || originalRoot is null)
        {
            return document.Project.Solution;
        }

        // Removing nodes leaves stray leading/trailing blank lines; normalise each file's text so the
        // output is clean (no extra blank lines) while preserving the file's existing newline style
        // and whether it ended with a trailing newline. This keeps the moved type's namespace,
        // usings and header intact. Newline/trailing state is read from the already-materialised
        // SourceText rather than re-serialising the whole green tree a third time.
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newLine = DetectNewLine(sourceText);
        var endsWithNewLine = sourceText.Length > 0 && sourceText[sourceText.Length - 1] == '\n';
        var extractedText = SourceText.From(Normalize(extractedRoot.ToFullString(), newLine, endsWithNewLine));
        var originalText = SourceText.From(Normalize(originalRoot.ToFullString(), newLine, endsWithNewLine));

        var solution = document.Project.Solution;

        // The same physical file is a linked document in every target-framework project. Add the new
        // file once per linked project (a fresh DocumentId each) so it becomes a single file linked
        // into all of them, and strip the moved type from every linked copy of the original — this is
        // what avoids the "one file per TFM" duplication seen with naive single-project fixes.
        solution = AddLinkedDocument(solution, document.Id, fileName, extractedText, document.Folders);
        solution = solution.WithDocumentText(document.Id, originalText);

        foreach (var linkedId in document.GetLinkedDocumentIds())
        {
            solution = AddLinkedDocument(solution, linkedId, fileName, extractedText, document.Folders);
            solution = solution.WithDocumentText(linkedId, originalText);
        }

        return solution;
    }

    /// <summary>Adds a new document mirroring an existing document's project and folders.</summary>
    /// <param name="solution">The solution to update.</param>
    /// <param name="siblingId">The id of an existing document in the target project.</param>
    /// <param name="fileName">The new file's name.</param>
    /// <param name="text">The new document's text.</param>
    /// <param name="folders">The folders the document lives in.</param>
    /// <returns>The updated solution.</returns>
    private static Solution AddLinkedDocument(Solution solution, DocumentId siblingId, string fileName, SourceText text, IReadOnlyList<string> folders)
    {
        var newId = DocumentId.CreateNewId(siblingId.ProjectId);
        return solution.AddDocument(newId, fileName, text, folders);
    }

    /// <summary>Detects the dominant newline of a source document from its first line break.</summary>
    /// <param name="text">The source text.</param>
    /// <returns><c>\r\n</c> when the first line ends with a carriage return + line feed; otherwise <c>\n</c>.</returns>
    private static string DetectNewLine(SourceText text)
    {
        if (text.Lines.Count <= 1)
        {
            return "\n";
        }

        const int carriageReturnLineFeedLength = 2;
        var first = text.Lines[0];
        return first.EndIncludingLineBreak - first.End == carriageReturnLineFeedLength ? "\r\n" : "\n";
    }

    /// <summary>Cleans the blank lines left at the removal seams, restoring the original file's trailing-newline state.</summary>
    /// <param name="text">The file text to normalize.</param>
    /// <param name="newLine">The newline sequence to join and terminate the file with.</param>
    /// <param name="endsWithNewLine">Whether the original file ended with a trailing newline.</param>
    /// <returns>The normalized text.</returns>
    /// <remarks>
    /// Removing nodes can leave a blank line after an opening brace, before a closing brace, or runs of
    /// blank lines — each of which would itself violate a layout rule (SST1505/SST1508/SST1507). Dropping
    /// only those keeps the moved type's own formatting intact while producing diagnostic-clean output.
    /// </remarks>
    private static string Normalize(string text, string newLine, bool endsWithNewLine)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            AppendNormalized(result, lines[index]);
        }

        while (result.Count > 0 && result[result.Count - 1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        var joined = string.Join(newLine, result);
        return endsWithNewLine ? joined + newLine : joined;
    }

    /// <summary>Appends a line to the accumulator, dropping the blank lines that removal leaves at the seams.</summary>
    /// <param name="result">The accumulated output lines.</param>
    /// <param name="line">The candidate line.</param>
    private static void AppendNormalized(List<string> result, string line)
    {
        if (line.Trim().Length == 0)
        {
            // Keep a blank only as a single separator between content lines (not leading, repeated, or after '{').
            if (ShouldKeepBlankSeparator(result))
            {
                result.Add(string.Empty);
            }

            return;
        }

        // Drop a blank line sitting immediately before a closing brace.
        if (result.Count > 0 && result[result.Count - 1].Length == 0 && line.TrimStart().StartsWith("}", StringComparison.Ordinal))
        {
            result.RemoveAt(result.Count - 1);
        }

        result.Add(line);
    }

    /// <summary>Returns whether a blank line should be kept as a separator after the lines emitted so far.</summary>
    /// <param name="result">The accumulated output lines.</param>
    /// <returns><see langword="true"/> when the preceding line is content not ending in an opening brace.</returns>
    private static bool ShouldKeepBlankSeparator(List<string> result)
    {
        if (result.Count == 0)
        {
            return false;
        }

        var last = result[result.Count - 1];
        return last.Length != 0 && !last.TrimEnd().EndsWith("{", StringComparison.Ordinal);
    }

    /// <summary>Clones the compilation unit, keeping only the moved type (and its enclosing namespaces, usings and header).</summary>
    /// <param name="compilationUnit">The original compilation unit.</param>
    /// <param name="type">The type declaration to keep.</param>
    /// <returns>The extracted root, or <see langword="null"/> when removal fails.</returns>
    private static CompilationUnitSyntax? BuildExtractedRoot(CompilationUnitSyntax compilationUnit, BaseTypeDeclarationSyntax type)
    {
        var topLevel = TypeFileNaming.TopLevelTypes(compilationUnit);
        var toRemove = new List<SyntaxNode>(topLevel.Count);
        for (var index = 0; index < topLevel.Count; index++)
        {
            if (!ReferenceEquals(topLevel[index], type))
            {
                toRemove.Add(topLevel[index]);
            }
        }

        return toRemove.Count == 0
            ? compilationUnit
            : compilationUnit.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepUnbalancedDirectives);
    }
}
