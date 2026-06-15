// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes an empty <c>else</c> clause from its <c>if</c> statement (SST1180).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyElseClauseCodeFixProvider))]
[Shared]
public sealed class EmptyElseClauseCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoEmptyElseClause.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ElseClauseSyntax>() is not { Parent: IfStatementSyntax ifStatement })
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the empty 'else' clause",
                    _ => Task.FromResult(Apply(context.Document, root, ifStatement)),
                    equivalenceKey: nameof(EmptyElseClauseCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ElseClauseSyntax>() is not { Parent: IfStatementSyntax ifStatement })
        {
            return;
        }

        // Drop any whitespace the then-branch left before 'else', then re-attach the statement's own
        // trailing trivia (what followed the else block) so the line break after the 'if' is preserved.
        var withoutElse = ifStatement
            .WithStatement(ifStatement.Statement.WithTrailingTrivia(SyntaxFactory.TriviaList()))
            .WithElse(null)
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia());

        editor.ReplaceNode(ifStatement, withoutElse);
    }

    /// <summary>Drops the empty <c>else</c> clause and any whitespace the <c>then</c> branch left trailing.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="ifStatement">The owning <c>if</c> statement.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, IfStatementSyntax ifStatement)
    {
        // Drop any whitespace the then-branch left before 'else', then re-attach the statement's own
        // trailing trivia (what followed the else block) so the line break after the 'if' is preserved.
        var withoutElse = ifStatement
            .WithStatement(ifStatement.Statement.WithTrailingTrivia(SyntaxFactory.TriviaList()))
            .WithElse(null)
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia());

        return document.WithSyntaxRoot(root.ReplaceNode(ifStatement, withoutElse));
    }
}
