// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a positional <c>ItemN</c> tuple access with the element's name (SST1142).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1142TupleElementNameCodeFixProvider))]
[Shared]
public sealed class Sst1142TupleElementNameCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly AsyncBatchEditFixAllProvider FixAll = new(RegisterEditsAsync);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ReferToTupleElementByName.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root
                .FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<IdentifierNameSyntax>() is not { Parent: MemberAccessExpressionSyntax access } identifier
                || !Sst1142TupleElementNameAnalyzer.TryGetReplacementName(access, semanticModel, context.CancellationToken, out var name)
                || string.IsNullOrEmpty(name))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Refer to the element as '{name}'",
                    cancellationToken => Task.FromResult(Replace(context.Document, root, identifier, name!)),
                    equivalenceKey: nameof(Sst1142TupleElementNameCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task RegisterEditsAsync(DocumentEditor editor, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var semanticModel = await editor.OriginalDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        if (editor.OriginalRoot
            .FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<IdentifierNameSyntax>() is not { Parent: MemberAccessExpressionSyntax access } identifier
            || !Sst1142TupleElementNameAnalyzer.TryGetReplacementName(access, semanticModel, cancellationToken, out var name)
            || string.IsNullOrEmpty(name))
        {
            return;
        }

        var renamed = identifier.WithIdentifier(SyntaxFactory.Identifier(identifier.Identifier.LeadingTrivia, name!, identifier.Identifier.TrailingTrivia));
        editor.ReplaceNode(identifier, renamed);
    }

    /// <summary>Replaces the tuple member name with the semantic element name.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="identifier">The positional tuple member name.</param>
    /// <param name="name">The semantic element name.</param>
    /// <returns>The updated document.</returns>
    internal static Document Replace(Document document, SyntaxNode root, IdentifierNameSyntax identifier, string name)
    {
        var renamed = identifier.WithIdentifier(SyntaxFactory.Identifier(identifier.Identifier.LeadingTrivia, name, identifier.Identifier.TrailingTrivia));
        return document.WithSyntaxRoot(root.ReplaceNode(identifier, renamed));
    }
}
