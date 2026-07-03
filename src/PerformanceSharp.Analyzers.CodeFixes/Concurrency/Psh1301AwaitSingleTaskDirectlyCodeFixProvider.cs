// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes a single-task <c>WhenAll</c>/<c>WaitAll</c> wrapper (PSH1301): a <c>WhenAll</c>
/// invocation is replaced with its task argument, and a <c>WaitAll</c> invocation becomes
/// <c>task.Wait()</c>. Non-primary argument expressions are parenthesized so the rewritten
/// expression parses the same way.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1301AwaitSingleTaskDirectlyCodeFixProvider))]
[Shared]
public sealed class Psh1301AwaitSingleTaskDirectlyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The name of the blocking wait method the WaitAll rewrite calls.</summary>
    private const string WaitMethodName = "Wait";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.AwaitSingleTaskDirectly.Id);

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
            if (TryGetCombinatorInvocation(root, diagnostic) is not { } invocation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the task directly",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, invocation)),
                    equivalenceKey: nameof(Psh1301AwaitSingleTaskDirectlyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetCombinatorInvocation(editor.OriginalRoot, diagnostic) is not { } invocation)
        {
            return;
        }

        editor.ReplaceNode(invocation, Rewrite(invocation));
    }

    /// <summary>Replaces the reported combinator invocation with the direct task form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The single-task combinator invocation to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)));

    /// <summary>Returns the reported combinator invocation when the diagnostic location covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The combinator invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetCombinatorInvocation(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1301AwaitSingleTaskDirectlyAnalyzer.IsSingleArgumentCombinatorShape(invocation, out _)
            ? invocation
            : null;

    /// <summary>Rewrites <c>Task.WhenAll(t)</c> to <c>t</c> and <c>Task.WaitAll(t)</c> to <c>t.Wait()</c>.</summary>
    /// <param name="invocation">The combinator invocation; callers must have validated the shape.</param>
    /// <returns>The rewritten expression.</returns>
    private static ExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var isWaitAll = access.Name.Identifier.ValueText == Psh1301AwaitSingleTaskDirectlyAnalyzer.WaitAllMethodName;
        var task = ParenthesizeIfNeeded(invocation.ArgumentList.Arguments[0].Expression.WithoutTrivia());
        var replacement = isWaitAll
            ? SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, task, SyntaxFactory.IdentifierName(WaitMethodName)))
            : task;

        return replacement.WithTriviaFrom(invocation);
    }

    /// <summary>Parenthesizes a task expression that is not a primary expression, so the rewrite parses the same way.</summary>
    /// <param name="expression">The task argument expression.</param>
    /// <returns>The expression, parenthesized when required.</returns>
    private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ParenthesizedExpressionSyntax
            ? expression
            : SyntaxFactory.ParenthesizedExpression(expression);
}
