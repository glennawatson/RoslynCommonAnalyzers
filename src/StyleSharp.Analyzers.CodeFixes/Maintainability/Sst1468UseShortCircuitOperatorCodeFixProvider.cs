// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a non-short-circuiting boolean <c>&amp;</c> / <c>|</c> with <c>&amp;&amp;</c> / <c>||</c> (SST1468).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1468UseShortCircuitOperatorCodeFixProvider))]
[Shared]
public sealed class Sst1468UseShortCircuitOperatorCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UseShortCircuitOperator.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax binary || !ShortCircuitOperatorRewrite.IsFixableKind(binary))
            {
                continue;
            }

            var title = binary.IsKind(SyntaxKind.BitwiseAndExpression) ? "Use '&&'" : "Use '||'";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(Apply(context.Document, root, binary)),
                    equivalenceKey: nameof(Sst1468UseShortCircuitOperatorCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax binary || !ShortCircuitOperatorRewrite.IsFixableKind(binary))
        {
            return;
        }

        editor.ReplaceNode(binary, (current, _) => ShortCircuitOperatorRewrite.Rewrite((BinaryExpressionSyntax)current));
    }

    /// <summary>Replaces the eager operator with its short-circuiting form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="binary">The reported <c>&amp;</c> / <c>|</c> expression.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax binary)
        => document.WithSyntaxRoot(root.ReplaceNode(binary, ShortCircuitOperatorRewrite.Rewrite(binary)));
}
