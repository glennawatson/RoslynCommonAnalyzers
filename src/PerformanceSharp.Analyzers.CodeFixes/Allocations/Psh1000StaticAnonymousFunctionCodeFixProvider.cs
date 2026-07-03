// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds the <c>static</c> modifier to a capture-free anonymous function (PSH1000),
/// moving the function's leading trivia onto the inserted keyword.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1000StaticAnonymousFunctionCodeFixProvider))]
[Shared]
public sealed class Psh1000StaticAnonymousFunctionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.MakeAnonymousFunctionStatic.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is not { } function)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make the anonymous function static",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, function)),
                    equivalenceKey: nameof(Psh1000StaticAnonymousFunctionCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is not { } function)
        {
            return;
        }

        editor.ReplaceNode(function, Rewrite(function));
    }

    /// <summary>Adds the <c>static</c> modifier to the reported anonymous function.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="function">The anonymous function to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, AnonymousFunctionExpressionSyntax function)
        => document.WithSyntaxRoot(root.ReplaceNode(function, Rewrite(function)));

    /// <summary>Inserts a leading <c>static</c> modifier, keeping the function's leading trivia on it.</summary>
    /// <param name="function">The anonymous function to rewrite.</param>
    /// <returns>The rewritten anonymous function.</returns>
    private static AnonymousFunctionExpressionSyntax Rewrite(AnonymousFunctionExpressionSyntax function)
    {
        var staticKeyword = SyntaxFactory.Token(SyntaxKind.StaticKeyword)
            .WithLeadingTrivia(function.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.Space);
        var stripped = function.WithLeadingTrivia(SyntaxFactory.TriviaList());
        return stripped.WithModifiers(stripped.Modifiers.Insert(0, staticKeyword));
    }
}
