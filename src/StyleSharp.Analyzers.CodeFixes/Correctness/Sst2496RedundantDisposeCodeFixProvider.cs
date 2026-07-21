// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes the redundant explicit disposal reported by SST2496, leaving the enclosing <c>using</c> to
/// dispose the value. Only a call that is its own expression statement is removed; an explicit disposal
/// woven into a larger expression is left for the author.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2496RedundantDisposeCodeFixProvider))]
[Shared]
public sealed class Sst2496RedundantDisposeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.RedundantDispose.Id);

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
            if (TryGetRedundantStatement(root, diagnostic) is not { } statement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant disposal",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia)!)),
                    nameof(Sst2496RedundantDisposeCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetRedundantStatement(editor.OriginalRoot, diagnostic) is not { } statement)
        {
            return;
        }

        editor.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <summary>Resolves the diagnostic to the expression statement that only makes the redundant call.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The removable statement, or <see langword="null"/> when the call is not its own statement.</returns>
    private static ExpressionStatementSyntax? TryGetRedundantStatement(SyntaxNode root, Diagnostic diagnostic)
    {
        return root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is { } invocation
            && invocation.Parent is ExpressionStatementSyntax statement
                ? statement
                : null;
    }
}
