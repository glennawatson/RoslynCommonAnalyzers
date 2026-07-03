// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes a useless <c>GC.SuppressFinalize(this)</c> call (PSH1008). The whole expression
/// statement is deleted; when the call is not a standalone statement no fix is offered.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1008UselessSuppressFinalizeCodeFixProvider))]
[Shared]
public sealed class Psh1008UselessSuppressFinalizeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.UselessSuppressFinalize.Id);

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
                    "Remove the SuppressFinalize call",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, statement!)),
                    equivalenceKey: nameof(Psh1008UselessSuppressFinalizeCodeFixProvider)),
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

        editor.RemoveNode(statement!, SyntaxRemoveOptions.KeepUnbalancedDirectives);
    }

    /// <summary>Removes the reported statement from the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="statement">The statement to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ExpressionStatementSyntax statement)
        => document.WithSyntaxRoot(root.RemoveNode(statement, SyntaxRemoveOptions.KeepUnbalancedDirectives) ?? root);

    /// <summary>Resolves the diagnostic to a standalone SuppressFinalize statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="statement">The removable statement when found.</param>
    /// <returns><see langword="true"/> when the call is a standalone statement.</returns>
    private static bool TryGetStatement(SyntaxNode root, Diagnostic diagnostic, out ExpressionStatementSyntax? statement)
    {
        statement = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>()?.Parent as ExpressionStatementSyntax;
        return statement is not null;
    }
}
