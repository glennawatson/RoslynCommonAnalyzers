// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant trailing <c>return;</c> or <c>continue;</c> statement (SST1174).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantJumpCodeFixProvider))]
[Shared]
public sealed class RedundantJumpCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantJump.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is not StatementSyntax statement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant statement",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, statement)),
                    equivalenceKey: nameof(RedundantJumpCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not StatementSyntax statement)
        {
            return;
        }

        editor.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <summary>Removes the redundant jump statement, dropping its line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="statement">The redundant statement to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, StatementSyntax statement)
    {
        var updated = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(updated!);
    }
}
