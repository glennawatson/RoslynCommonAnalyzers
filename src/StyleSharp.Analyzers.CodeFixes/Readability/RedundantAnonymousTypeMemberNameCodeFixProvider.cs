// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant anonymous-type member name (SST1173), keeping the inferred name.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantAnonymousTypeMemberNameCodeFixProvider))]
[Shared]
public sealed class RedundantAnonymousTypeMemberNameCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantAnonymousTypeMemberName.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<AnonymousObjectMemberDeclaratorSyntax>() is not { } declarator)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Omit the redundant member name",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, declarator)),
                    equivalenceKey: nameof(RedundantAnonymousTypeMemberNameCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<AnonymousObjectMemberDeclaratorSyntax>() is not { } declarator)
        {
            return;
        }

        var expression = declarator.Expression.WithTriviaFrom(declarator);
        editor.ReplaceNode(declarator, declarator.WithNameEquals(null).WithExpression(expression));
    }

    /// <summary>Removes the explicit name from the anonymous-type member declarator.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declarator">The member declarator whose name is redundant.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, AnonymousObjectMemberDeclaratorSyntax declarator)
    {
        var expression = declarator.Expression.WithTriviaFrom(declarator);
        var replacement = declarator.WithNameEquals(null).WithExpression(expression);
        return document.WithSyntaxRoot(root.ReplaceNode(declarator, replacement));
    }
}
