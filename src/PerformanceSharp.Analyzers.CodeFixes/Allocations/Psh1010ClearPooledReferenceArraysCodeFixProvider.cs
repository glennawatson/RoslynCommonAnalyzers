// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Supplies <c>clearArray: true</c> on a reported <c>ArrayPool&lt;T&gt;.Return</c> call
/// (PSH1010): appended as a named argument when the flag is absent, or substituted for the
/// existing constant-false value while keeping its argument shape.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1010ClearPooledReferenceArraysCodeFixProvider))]
[Shared]
public sealed class Psh1010ClearPooledReferenceArraysCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.ClearPooledReferenceArrays.Id);

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
            if (TryGetReturnInvocation(root, diagnostic) is not { } invocation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Clear the array on return",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)))),
                    equivalenceKey: nameof(Psh1010ClearPooledReferenceArraysCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetReturnInvocation(editor.OriginalRoot, diagnostic) is not { } invocation)
        {
            return;
        }

        editor.ReplaceNode(invocation, Rewrite(invocation));
    }

    /// <summary>Returns the reported return invocation when the diagnostic location still covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetReturnInvocation(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation
            && access.Name.Identifier.ValueText == Psh1010ClearPooledReferenceArraysAnalyzer.ReturnMethodName
            ? invocation
            : null;

    /// <summary>Builds the invocation with <c>clearArray: true</c> supplied.</summary>
    /// <param name="invocation">The return invocation to rewrite.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var trueExpression = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var isClearArgument = argument.NameColon is { } nameColon
                ? nameColon.Name.Identifier.ValueText == Psh1010ClearPooledReferenceArraysAnalyzer.ClearArrayParameterName
                : i == 1;
            if (!isClearArgument)
            {
                continue;
            }

            return invocation.ReplaceNode(argument.Expression, trueExpression.WithTriviaFrom(argument.Expression));
        }

        var namedArgument = SyntaxFactory.Argument(
            SyntaxFactory.NameColon(Psh1010ClearPooledReferenceArraysAnalyzer.ClearArrayParameterName),
            default,
            trueExpression);
        return invocation.WithArgumentList(invocation.ArgumentList.AddArguments(namedArgument));
    }
}
