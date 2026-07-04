// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported <c>ContainsKey</c>-guarded indexer write to a single
/// <c>TryAdd(key, value)</c> call (PSH1115). Only the TryAdd shape is fixed automatically —
/// the value expression moves out of the guarded branch, so it is evaluated even when the key
/// exists, which matches the framework guidance for TryAdd. The value-slot shape is reported
/// without a fix because rewriting it needs a ref local the surrounding code must adopt.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1115SingleProbeInsertCodeFixProvider))]
[Shared]
public sealed class Psh1115SingleProbeInsertCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.SingleProbeInsert.Id);

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
            if (TryRewrite(root, diagnostic) is not { } rewrite)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use TryAdd",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(rewrite.Original, rewrite.Replacement))),
                    equivalenceKey: nameof(Psh1115SingleProbeInsertCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryRewrite(editor.OriginalRoot, diagnostic) is not { } rewrite)
        {
            return;
        }

        editor.ReplaceNode(rewrite.Original, rewrite.Replacement);
    }

    /// <summary>Resolves the reported guard and builds the TryAdd statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The original if statement and its replacement, or <see langword="null"/> for the value-slot shape.</returns>
    private static (IfStatementSyntax Original, StatementSyntax Replacement)? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not IfStatementSyntax ifStatement
            || Psh1115SingleProbeInsertAnalyzer.TryGetNegatedGuard(
                ifStatement,
                Psh1115SingleProbeInsertAnalyzer.ContainsKeyMethodName,
                argumentCount: 1) is not { } guard
            || Psh1115SingleProbeInsertAnalyzer.TryGetGuardedIndexerStore(ifStatement, guard.Receiver, guard.Key) is not { } value)
        {
            return null;
        }

        var tryAdd = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                guard.Receiver.WithoutTrivia(),
                SyntaxFactory.IdentifierName(Psh1115SingleProbeInsertAnalyzer.TryAddMethodName)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(ImmutableArrays.Of(
                SyntaxFactory.Argument(guard.Key.WithoutTrivia()),
                SyntaxFactory.Argument(value.WithoutTrivia()).WithLeadingTrivia(SyntaxFactory.Space)))));

        return (ifStatement, SyntaxFactory.ExpressionStatement(tryAdd).WithTriviaFrom(ifStatement));
    }
}
