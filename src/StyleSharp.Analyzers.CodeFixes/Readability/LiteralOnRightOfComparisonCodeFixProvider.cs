// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Swaps a comparison so the literal sits on the right (SST1186).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LiteralOnRightOfComparisonCodeFixProvider))]
[Shared]
public sealed class LiteralOnRightOfComparisonCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.LiteralOnRightOfComparison.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax comparison)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Place the literal on the right",
                    _ => Task.FromResult(Apply(context.Document, root, comparison)),
                    equivalenceKey: nameof(LiteralOnRightOfComparisonCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax comparison)
        {
            return;
        }

        var newLeft = comparison.Right.WithTriviaFrom(comparison.Left);
        var newRight = comparison.Left.WithTriviaFrom(comparison.Right);
        editor.ReplaceNode(comparison, comparison.WithLeft(newLeft).WithRight(newRight));
    }

    /// <summary>Swaps the operands, keeping the surrounding spacing in place.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The comparison whose literal is on the left.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax comparison)
    {
        var newLeft = comparison.Right.WithTriviaFrom(comparison.Left);
        var newRight = comparison.Left.WithTriviaFrom(comparison.Right);
        var replacement = comparison.WithLeft(newLeft).WithRight(newRight);

        return document.WithSyntaxRoot(root.ReplaceNode(comparison, replacement));
    }
}
