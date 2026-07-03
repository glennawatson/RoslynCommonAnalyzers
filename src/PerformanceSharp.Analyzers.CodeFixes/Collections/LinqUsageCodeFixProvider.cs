// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Collapses redundant LINQ chain layers: moves a <c>Where</c> predicate into the
/// terminal call (PSH1101) and replaces a type-check <c>Where</c> plus <c>Cast</c>
/// with one <c>OfType</c> call (PSH1102). PSH1100 (avoid LINQ on hot paths) has no
/// automated fix because a LINQ-to-loop rewrite is not mechanically safe.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LinqUsageCodeFixProvider))]
[Shared]
public sealed class LinqUsageCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        CollectionRules.CollapseLinqWhereTerminal.Id,
        CollectionRules.CollapseLinqTypeFilter.Id);

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
            var replacement = CreateEdit(root, diagnostic, out var oldNode);
            if (replacement is null || oldNode is null)
            {
                continue;
            }

            var title = diagnostic.Id == CollectionRules.CollapseLinqWhereTerminal.Id
                ? "Move predicate to terminal call"
                : "Use one typed filter";
            var document = context.Document;
            var currentDiagnostic = diagnostic;
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(Apply(document, root, currentDiagnostic)),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var replacement = CreateEdit(editor.OriginalRoot, diagnostic, out var oldNode);
        if (replacement is null || oldNode is null)
        {
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
    }

    /// <summary>Applies the collapse for one diagnostic to a document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original when no edit applies.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var replacement = CreateEdit(root, diagnostic, out var oldNode);
        return replacement is null || oldNode is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(oldNode, replacement));
    }

    /// <summary>Creates the collapsed invocation for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The invocation to replace.</param>
    /// <returns>The replacement invocation, or <see langword="null"/>.</returns>
    private static InvocationExpressionSyntax? CreateEdit(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode)
    {
        oldNode = null;
        return diagnostic.Id == CollectionRules.CollapseLinqWhereTerminal.Id
            ? CreateWhereTerminalFix(root, diagnostic.Location.SourceSpan, out oldNode)
            : CreateTypeFilterFix(root, diagnostic.Location.SourceSpan, out oldNode);
    }

    /// <summary>Creates a collapsed <c>Where(predicate).Terminal()</c> invocation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The terminal invocation to replace.</param>
    /// <returns>The collapsed invocation.</returns>
    private static InvocationExpressionSyntax? CreateWhereTerminalFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var invocation = root.FindNode(span).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is not { ArgumentList.Arguments.Count: 0, Expression: MemberAccessExpressionSyntax outerAccess }
            || outerAccess.Expression is not InvocationExpressionSyntax whereInvocation
            || whereInvocation is not
            {
                ArgumentList.Arguments.Count: 1,
                Expression: MemberAccessExpressionSyntax { Expression: { } receiver }
            })
        {
            return null;
        }

        oldNode = invocation;
        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            receiver.WithoutTrivia(),
            outerAccess.Name.WithoutTrivia());
        return invocation
            .WithExpression(memberAccess)
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(whereInvocation.ArgumentList.Arguments[0].WithoutTrivia())))
            .WithTriviaFrom(invocation);
    }

    /// <summary>Creates a collapsed <c>OfType&lt;T&gt;</c> invocation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The cast invocation to replace.</param>
    /// <returns>The collapsed invocation.</returns>
    private static InvocationExpressionSyntax? CreateTypeFilterFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var invocation = root.FindNode(span).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is not
            {
                ArgumentList.Arguments.Count: 0,
                Expression: MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax { TypeArgumentList: { } typeArguments },
                    Expression: InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax { Expression: { } receiver }
                    }
                }
            })
        {
            return null;
        }

        oldNode = invocation;
        var ofTypeName = SyntaxFactory.GenericName(SyntaxFactory.Identifier("OfType")).WithTypeArgumentList(typeArguments.WithoutTrivia());
        var memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver.WithoutTrivia(), ofTypeName);
        return invocation.WithExpression(memberAccess).WithTriviaFrom(invocation);
    }
}
