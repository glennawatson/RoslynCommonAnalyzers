// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces narrow builder sequences with a collection expression.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionExpressionBuilderCodeFixProvider))]
[Shared]
public sealed class CollectionExpressionBuilderCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(CollectionExpressionRules.UseCollectionExpressionForBuilder.Id);

    /// <inheritdoc/>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            if (FindLocal(root, diagnostic.Location.SourceSpan) is not { } local
                || BuildReplacementRoot(root, local) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use collection expression",
                    _ => Task.FromResult(Apply(context.Document, root, local)),
                    equivalenceKey: CollectionExpressionRules.UseCollectionExpressionForBuilder.Id),
                diagnostic);
        }
    }

    /// <summary>Applies the builder sequence replacement.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The root.</param>
    /// <param name="local">The builder local.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, LocalDeclarationStatementSyntax local)
    {
        var updated = BuildReplacementRoot(root, local);
        return updated is null ? document : document.WithSyntaxRoot(updated);
    }

    /// <summary>Builds a root with the builder sequence replaced by a single return statement.</summary>
    /// <param name="root">The root.</param>
    /// <param name="local">The builder local.</param>
    /// <returns>The updated root, or <see langword="null"/>.</returns>
    private static SyntaxNode? BuildReplacementRoot(SyntaxNode root, LocalDeclarationStatementSyntax local)
    {
        if (local.Parent is not BlockSyntax block
            || !CollectionExpressionAdvancedAnalysis.TryGetBuilderSequence(local, out var elements, out var returnStatement))
        {
            return null;
        }

        var start = block.Statements.IndexOf(local);
        var end = block.Statements.IndexOf(returnStatement);
        if (start < 0 || end <= start)
        {
            return null;
        }

        var replacement = SyntaxFactory.ParseStatement("return " + CollectionExpressionText(elements) + ";")
            .WithTriviaFrom(local);
        var statements = ReplaceStatementRange(block.Statements, start, end, replacement);
        return root.ReplaceNode(block, block.WithStatements(statements));
    }

    /// <summary>Replaces a contiguous statement range with a single statement.</summary>
    /// <param name="statements">The original statements.</param>
    /// <param name="start">The first statement index.</param>
    /// <param name="end">The last statement index.</param>
    /// <param name="replacement">The replacement statement.</param>
    /// <returns>The updated statement list.</returns>
    private static SyntaxList<StatementSyntax> ReplaceStatementRange(
        SyntaxList<StatementSyntax> statements,
        int start,
        int end,
        StatementSyntax replacement)
    {
        var updated = new StatementSyntax[statements.Count - (end - start)];
        var write = 0;
        for (var i = 0; i < statements.Count; i++)
        {
            if (i == start)
            {
                updated[write] = replacement;
                write++;
                continue;
            }

            if (i <= end)
            {
                continue;
            }

            updated[write] = statements[i];
            write++;
        }

        return SyntaxFactory.List(updated);
    }

    /// <summary>Builds a collection expression from builder Add call arguments.</summary>
    /// <param name="elements">The element expressions.</param>
    /// <returns>The collection expression text.</returns>
    private static string CollectionExpressionText(ExpressionSyntax[] elements)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append('[');
        for (var i = 0; i < elements.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(elements[i].WithoutTrivia());
        }

        builder.Append(']');
        return builder.ToString();
    }

    /// <summary>Finds the local declaration reported by the diagnostic.</summary>
    /// <param name="root">The root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The local declaration, or <see langword="null"/>.</returns>
    private static LocalDeclarationStatementSyntax? FindLocal(SyntaxNode root, TextSpan span)
        => root.FindToken(span.Start).Parent?.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
}
