// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a composite <c>string.Format</c> call or a literal-plus-value concatenation as the
/// interpolated string that says the same thing (SST2249). The replacement is the node the analyzer
/// already proved compiles to the same value, carrying the original expression's trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2249UseInterpolatedStringCodeFixProvider))]
[Shared]
public sealed class Sst2249UseInterpolatedStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseInterpolatedString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryCreateReplacement(root, model, diagnostic, context.CancellationToken, out var original, out var replacement))
            {
                continue;
            }

            var target = original;
            var rewritten = replacement;
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use an interpolated string",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(target!, rewritten!))),
                    equivalenceKey: nameof(Sst2249UseInterpolatedStringCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryCreateReplacement(editor.OriginalRoot, editor.SemanticModel, diagnostic, CancellationToken.None, out var original, out var replacement))
        {
            return;
        }

        editor.ReplaceNode(original!, replacement!);
    }

    /// <summary>Resolves the reported node and builds its interpolated-string replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="original">The node to replace.</param>
    /// <param name="replacement">The interpolated string, carrying the original's trivia.</param>
    /// <returns><see langword="true"/> when a verified rewrite exists.</returns>
    private static bool TryCreateReplacement(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        CancellationToken cancellationToken,
        out SyntaxNode? original,
        out SyntaxNode? replacement)
    {
        original = null;
        replacement = null;
        switch (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true))
        {
            case InvocationExpressionSyntax invocation when InterpolatedStringConversion.TryConvertFormat(model, invocation, cancellationToken) is { } formatted:
            {
                original = invocation;
                replacement = formatted.WithTriviaFrom(invocation);
                return true;
            }

            case BinaryExpressionSyntax binary when InterpolatedStringConversion.TryConvertConcatenation(model, binary, cancellationToken) is { } concatenated:
            {
                original = binary;
                replacement = concatenated.WithTriviaFrom(binary);
                return true;
            }

            default:
                return false;
        }
    }
}
