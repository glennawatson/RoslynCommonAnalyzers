// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a self-assignment statement (SST1189).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SelfAssignmentCodeFixProvider))]
[Shared]
public sealed class SelfAssignmentCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoSelfAssignment.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax statement })
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the self-assignment",
                    _ => Task.FromResult(Apply(context.Document, root, statement)),
                    equivalenceKey: nameof(SelfAssignmentCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax statement })
        {
            return;
        }

        editor.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <summary>Removes the self-assignment statement, dropping its line.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="statement">The self-assignment statement.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ExpressionStatementSyntax statement)
    {
        var updated = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(updated!);
    }
}
