// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Throws the exception the statement only constructed (SST1480), turning <c>new InvalidOperationException();</c>
/// into <c>throw new InvalidOperationException();</c>.
/// </summary>
/// <remarks>
/// The forgotten <c>throw</c> is the overwhelmingly likely intent, so the fix restores it rather than deleting
/// the statement; a reader who meant to delete it can still do so, but a reader who meant to throw would
/// otherwise lose the exception and its arguments.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1480ExceptionNeverThrownCodeFixProvider))]
[Shared]
public sealed class Sst1480ExceptionNeverThrownCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.ExceptionNeverThrown.Id);

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
            if (!TryGetStatement(root, diagnostic, out var statement))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Throw the exception",
                    _ => Task.FromResult(Apply(context.Document, root, statement!)),
                    equivalenceKey: nameof(Sst1480ExceptionNeverThrownCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetStatement(editor.OriginalRoot, diagnostic, out var statement))
        {
            return;
        }

        editor.ReplaceNode(statement!, (current, _) => BuildThrow((ExpressionStatementSyntax)current));
    }

    /// <summary>Rewrites the discarded creation as a throw statement.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="statement">The statement that only constructs the exception.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ExpressionStatementSyntax statement)
        => document.WithSyntaxRoot(root.ReplaceNode(statement, BuildThrow(statement)));

    /// <summary>Resolves the diagnostic's span back to the statement that discards the exception.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="statement">The reported statement when found.</param>
    /// <returns><see langword="true"/> when the reported shape still matches.</returns>
    private static bool TryGetStatement(SyntaxNode root, Diagnostic diagnostic, out ExpressionStatementSyntax? statement)
    {
        statement = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BaseObjectCreationExpressionSyntax creation
            ? creation.Parent as ExpressionStatementSyntax
            : null;
        return statement is not null;
    }

    /// <summary>Builds the throw statement, keeping the statement's indentation and its trailing trivia.</summary>
    /// <param name="statement">The statement that only constructs the exception.</param>
    /// <returns>The throw statement.</returns>
    /// <remarks>
    /// The statement's leading trivia lives on the <c>new</c> keyword, so it moves to the <c>throw</c> keyword
    /// that now starts the line; the original semicolon carries the trailing trivia across untouched.
    /// </remarks>
    private static ThrowStatementSyntax BuildThrow(ExpressionStatementSyntax statement)
        => SyntaxFactory.ThrowStatement(statement.Expression.WithLeadingTrivia(SyntaxFactory.TriviaList()))
            .WithThrowKeyword(SyntaxFactory.Token(SyntaxKind.ThrowKeyword)
                .WithLeadingTrivia(statement.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.Space))
            .WithSemicolonToken(statement.SemicolonToken);
}
