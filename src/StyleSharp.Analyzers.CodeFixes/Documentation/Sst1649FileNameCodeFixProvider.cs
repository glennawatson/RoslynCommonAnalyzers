// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>
/// Renames a file whose name does not match its first declared type (SST1649) so the name
/// matches. The file content is left untouched (no semantic model, formatter or simplifier
/// runs); only the document name changes. The rename is applied to every linked target-framework
/// copy at once, and the new name follows the configured generic convention (<c>Widget{T}.cs</c>
/// by default, <c>Widget`1.cs</c> under <c>metadata</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1649FileNameCodeFixProvider))]
[Shared]
public sealed class Sst1649FileNameCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.FileNameMatchesType.Id);

    /// <inheritdoc/>
    /// <remarks>FixAll is disabled: this fix renames a document (remove + add), which the batch fixer
    /// (text-edit merging only) cannot carry, so each occurrence is fixed individually.</remarks>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var tree = await context.Document.GetSyntaxTreeAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || tree is null)
        {
            return;
        }

        var options = context.Document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree);
        var useMetadata = TypeFileNaming.UseMetadataConvention(options, DocumentationRules.FileNameMatchesType.Id);

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
            {
                continue;
            }

            var fileName = TypeFileNaming.Stem(member, useMetadata) + ".cs";
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename file to '{fileName}'",
                    cancellationToken => RenameAsync(context.Document, fileName, cancellationToken),
                    equivalenceKey: nameof(Sst1649FileNameCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Renames the document (and every linked copy) to the new file name, preserving its contents.</summary>
    /// <param name="document">The document to rename.</param>
    /// <param name="fileName">The new file name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated solution.</returns>
    internal static async Task<Solution> RenameAsync(Document document, string fileName, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document.Project.Solution;
        }

        // Capture every project that links this file before mutating, since removing a document
        // invalidates the linked ids. The same physical file is recreated under the new name in each
        // project so the rename stays a single linked file rather than diverging per framework.
        var projectIds = new List<ProjectId> { document.Project.Id };
        var folders = document.Folders;
        foreach (var linkedId in document.GetLinkedDocumentIds())
        {
            projectIds.Add(linkedId.ProjectId);
        }

        var solution = document.Project.Solution;
        solution = solution.RemoveDocument(document.Id);
        foreach (var linkedId in document.GetLinkedDocumentIds())
        {
            solution = solution.RemoveDocument(linkedId);
        }

        for (var index = 0; index < projectIds.Count; index++)
        {
            var newId = DocumentId.CreateNewId(projectIds[index]);
            solution = solution.AddDocument(newId, fileName, root, folders);
        }

        return solution;
    }
}
