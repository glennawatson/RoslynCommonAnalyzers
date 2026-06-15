// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Replaces an empty string literal with <c>string.Empty</c> (SST1122).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1122UseStringEmptyCodeFixProvider))]
[Shared]
public sealed class Sst1122UseStringEmptyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseStringEmpty.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not LiteralExpressionSyntax literal)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use string.Empty",
                    _ => Task.FromResult(Replace(context.Document, root, literal)),
                    equivalenceKey: nameof(Sst1122UseStringEmptyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not LiteralExpressionSyntax literal)
        {
            return;
        }

        var replacement = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName("Empty"))
            .WithTriviaFrom(literal);

        editor.ReplaceNode(literal, replacement);
    }

    /// <summary>Replaces the empty string literal with a <c>string.Empty</c> member access.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="literal">The empty string literal.</param>
    /// <returns>The updated document.</returns>
    internal static Document Replace(Document document, SyntaxNode root, LiteralExpressionSyntax literal)
    {
        var replacement = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName("Empty"))
            .WithTriviaFrom(literal);

        return document.WithSyntaxRoot(root.ReplaceNode(literal, replacement));
    }
}
