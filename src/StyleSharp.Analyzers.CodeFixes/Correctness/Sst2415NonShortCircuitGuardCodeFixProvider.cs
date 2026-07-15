// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Short-circuits a boolean <c>&amp;</c> / <c>|</c> whose right operand does work (SST2415), so the left
/// operand actually guards it. The fix title states plainly that this changes behaviour — the right operand
/// stops running when the left decides the result.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2415NonShortCircuitGuardCodeFixProvider))]
[Shared]
public sealed class Sst2415NonShortCircuitGuardCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.NonShortCircuitGuard.Id);

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

            var replacement = binary.IsKind(SyntaxKind.BitwiseAndExpression) ? "&&" : "||";
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Short-circuit with '{replacement}' — the right operand will no longer run when the left decides",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(binary, ShortCircuitOperatorRewrite.Rewrite(binary)))),
                    equivalenceKey: nameof(Sst2415NonShortCircuitGuardCodeFixProvider)),
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
}
