// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported <c>Task.FromResult(x)</c> call to <c>Task.CompletedTask</c> (PSH1308).
/// The original receiver expression carries over unchanged, so an alias or a fully qualified
/// spelling stays as the author wrote it. The dropped argument is a value the caller can never
/// observe; the analyzer only reports calls consumed as the non-generic task.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1308CompletedTaskCodeFixProvider))]
[Shared]
public sealed class Psh1308CompletedTaskCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.UseCompletedTask.Id);

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
            if (TryGetFromResultInvocation(root, diagnostic) is not { } invocation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use Task.CompletedTask",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)))),
                    equivalenceKey: nameof(Psh1308CompletedTaskCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetFromResultInvocation(editor.OriginalRoot, diagnostic) is not { } invocation)
        {
            return;
        }

        editor.ReplaceNode(invocation, Rewrite(invocation));
    }

    /// <summary>Returns the reported FromResult invocation when its shape still matches.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetFromResultInvocation(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1308CompletedTaskAnalyzer.IsTaskFromResultShape(invocation)
            ? invocation
            : null;

    /// <summary>Builds the <c>Task.CompletedTask</c> replacement, reusing the original receiver spelling.</summary>
    /// <param name="invocation">The FromResult invocation to rewrite.</param>
    /// <returns>The property access replacement carrying the invocation's trivia.</returns>
    private static MemberAccessExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                access.Expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName(Psh1308CompletedTaskAnalyzer.CompletedTaskPropertyName))
            .WithTriviaFrom(invocation);
    }
}
