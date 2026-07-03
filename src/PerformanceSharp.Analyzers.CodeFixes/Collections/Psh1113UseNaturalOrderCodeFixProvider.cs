// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites an identity-selector sort to the natural form (PSH1113): <c>OrderBy(x =&gt; x)</c>
/// becomes <c>Order()</c> and <c>OrderByDescending(x =&gt; x)</c> becomes
/// <c>OrderDescending()</c>. A trailing comparer argument is preserved; explicit generic
/// arguments are dropped because the natural overload infers its one type argument from the
/// receiver.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1113UseNaturalOrderCodeFixProvider))]
[Shared]
public sealed class Psh1113UseNaturalOrderCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseNaturalOrder.Id);

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
            if (TryGetSortInvocation(root, diagnostic) is not { } invocation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Sort naturally",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)))),
                    equivalenceKey: nameof(Psh1113UseNaturalOrderCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetSortInvocation(editor.OriginalRoot, diagnostic) is not { } invocation)
        {
            return;
        }

        editor.ReplaceNode(invocation, Rewrite(invocation));
    }

    /// <summary>Returns the reported sort invocation when the diagnostic location still covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetSortInvocation(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                return Psh1113UseNaturalOrderAnalyzer.IsIdentitySortShape(invocation) ? invocation : null;
            }
        }

        return null;
    }

    /// <summary>Builds the natural-sort invocation, dropping the identity selector.</summary>
    /// <param name="invocation">The sort invocation to rewrite; callers must have validated the shape.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var isDescending = access.Name.Identifier.ValueText == Psh1113UseNaturalOrderAnalyzer.OrderByDescendingMethodName;
        var newName = SyntaxFactory.IdentifierName(
                isDescending ? Psh1113UseNaturalOrderAnalyzer.OrderDescendingMethodName : Psh1113UseNaturalOrderAnalyzer.OrderMethodName)
            .WithTriviaFrom(access.Name);

        var arguments = invocation.ArgumentList.Arguments;
        var newArguments = arguments.Count == 2
            ? SyntaxFactory.SingletonSeparatedList(arguments[1].WithoutTrivia())
            : default;

        return invocation
            .WithExpression(access.WithName(newName))
            .WithArgumentList(invocation.ArgumentList.WithArguments(newArguments));
    }
}
