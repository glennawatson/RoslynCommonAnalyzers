// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an unnecessary cast (SST1175), keeping the expression it applied to. The rule reports three
/// shapes — a cast expression, an <c>as</c> test, and a sequence re-typing call — and each collapses to
/// the operand or receiver it was wrapping.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantCastCodeFixProvider))]
[Shared]
public sealed class RedundantCastCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantCast.Id);

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
            if (Resolve(root, diagnostic) is not var (reported, kept))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the unnecessary cast",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(reported, kept.WithTriviaFrom(reported)))),
                    equivalenceKey: nameof(RedundantCastCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The replacement is recomputed from the node as it stands after nested edits are composed, so an
    /// outer conversion wrapping an inner one resolves in a single pass instead of surviving into another.
    /// </remarks>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not var (reported, _))
        {
            return;
        }

        editor.ReplaceNode(reported, static (current, _) => KeptExpression(current).WithTriviaFrom(current));
    }

    /// <summary>Replaces the cast expression with its operand.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="cast">The redundant cast expression.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, CastExpressionSyntax cast)
    {
        var replacement = cast.Expression.WithTriviaFrom(cast);
        return document.WithSyntaxRoot(root.ReplaceNode(cast, replacement));
    }

    /// <summary>Resolves the reported node and the expression that survives removing the conversion.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace and its replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static (SyntaxNode Reported, ExpressionSyntax Kept)? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node.FirstAncestorOrSelf<CastExpressionSyntax>() is { } cast)
        {
            return (cast, cast.Expression);
        }

        if (node.FirstAncestorOrSelf<BinaryExpressionSyntax>() is { } asExpression && asExpression.IsKind(SyntaxKind.AsExpression))
        {
            return (asExpression, asExpression.Left);
        }

        return node.FirstAncestorOrSelf<InvocationExpressionSyntax>() is { Expression: MemberAccessExpressionSyntax memberAccess } invocation
            ? (invocation, memberAccess.Expression)
            : null;
    }

    /// <summary>Returns the expression a reported conversion was wrapping.</summary>
    /// <param name="conversion">The reported conversion node.</param>
    /// <returns>The operand or receiver that survives removing it.</returns>
    private static ExpressionSyntax KeptExpression(SyntaxNode conversion) => conversion switch
    {
        CastExpressionSyntax cast => cast.Expression,
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) => binary.Left,
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => memberAccess.Expression,
        _ => (ExpressionSyntax)conversion,
    };
}
