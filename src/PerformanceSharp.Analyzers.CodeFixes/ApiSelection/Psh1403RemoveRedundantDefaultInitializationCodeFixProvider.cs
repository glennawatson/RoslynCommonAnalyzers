// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Removes a field initializer that restates the type's default value (PSH1403).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1403RemoveRedundantDefaultInitializationCodeFixProvider))]
[Shared]
public sealed class Psh1403RemoveRedundantDefaultInitializationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.RemoveRedundantDefaultInitialization.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<VariableDeclaratorSyntax>() is not { } declarator)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant initializer",
                    _ => Task.FromResult(Apply(context.Document, root, declarator)),
                    equivalenceKey: nameof(Psh1403RemoveRedundantDefaultInitializationCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<VariableDeclaratorSyntax>() is not { } declarator)
        {
            return;
        }

        editor.ReplaceNode(declarator, Rewrite(declarator));
    }

    /// <summary>Removes the reported declarator's initializer, leaving other declarators intact.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declarator">The variable declarator to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, VariableDeclaratorSyntax declarator)
        => document.WithSyntaxRoot(root.ReplaceNode(declarator, Rewrite(declarator)));

    /// <summary>Drops the initializer while keeping the declarator's trailing trivia.</summary>
    /// <param name="declarator">The variable declarator to rewrite.</param>
    /// <returns>The rewritten declarator.</returns>
    private static VariableDeclaratorSyntax Rewrite(VariableDeclaratorSyntax declarator)
        => declarator.WithInitializer(null).WithTrailingTrivia(declarator.GetTrailingTrivia());
}
