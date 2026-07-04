// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes a reported intermediate <c>ToString()</c> call (PSH1211), leaving its receiver in
/// place. In an argument position overload resolution then picks the direct overload the
/// analyzer proved exists; in an interpolation hole the handler formats the value in place.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1211RemoveIntermediateToStringCodeFixProvider))]
[Shared]
public sealed class Psh1211RemoveIntermediateToStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.RemoveIntermediateToString.Id);

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
            if (TryGetToStringCall(root, diagnostic) is not { } invocation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Pass the value directly",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)))),
                    equivalenceKey: nameof(Psh1211RemoveIntermediateToStringCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetToStringCall(editor.OriginalRoot, diagnostic) is not { } invocation)
        {
            return;
        }

        editor.ReplaceNode(invocation, Rewrite(invocation));
    }

    /// <summary>Returns the reported ToString invocation when its shape still matches.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetToStringCall(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation
            && Psh1211RemoveIntermediateToStringAnalyzer.IsBareToStringShape(invocation)
            ? invocation
            : null;

    /// <summary>Builds the replacement: the ToString receiver by itself.</summary>
    /// <param name="invocation">The ToString invocation.</param>
    /// <returns>The receiver expression carrying the invocation's trivia.</returns>
    private static ExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
        => ((MemberAccessExpressionSyntax)invocation.Expression).Expression.WithTriviaFrom(invocation);
}
