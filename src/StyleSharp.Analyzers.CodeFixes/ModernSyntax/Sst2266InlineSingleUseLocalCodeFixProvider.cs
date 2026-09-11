// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Inlines a single-use local into its one read (SST2266): the initializer replaces the reference — wrapped in
/// parentheses when its precedence could otherwise change — and the declaration statement is removed.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2266InlineSingleUseLocalCodeFixProvider))]
[Shared]
public sealed class Sst2266InlineSingleUseLocalCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.InlineSingleUseLocal.Id);

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
            if (Resolve(root, model, diagnostic) is not { } edit)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Inline the local into its use",
                    cancellationToken => ApplyAsync(context.Document, edit, cancellationToken),
                    equivalenceKey: nameof(Sst2266InlineSingleUseLocalCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, editor.SemanticModel, diagnostic) is not { } edit)
        {
            return;
        }

        editor.ReplaceNode(edit.Reference, edit.Inlined);
        editor.RemoveNode(edit.Declaration, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <summary>Applies one inline by replacing the reference and removing the declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="edit">The resolved edit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ApplyAsync(Document document, InlineEdit edit, CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.ReplaceNode(edit.Reference, edit.Inlined);
        editor.RemoveNode(edit.Declaration, SyntaxRemoveOptions.KeepNoTrivia);
        return editor.GetChangedDocument();
    }

    /// <summary>Resolves the reported declaration into the reference, its replacement, and the statement to drop.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The edit, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InlineEdit? Resolve(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is not { Parent: BlockSyntax block } local
            || local.Declaration is not { Variables.Count: 1 } declaration
            || declaration.Variables[0].Initializer is not { } equalsValue
            || !Sst2266InlineSingleUseLocalAnalyzer.IsPureInlinable(equalsValue.Value)
            || model.GetDeclaredSymbol(declaration.Variables[0]) is not ILocalSymbol symbol
            || !Sst2266InlineSingleUseLocalAnalyzer.PreservesDeclaredMeaning(model, equalsValue.Value, symbol, CancellationToken.None)
            || Sst2266InlineSingleUseLocalAnalyzer.FindSingleReference(model, block, symbol) is not { } reference
            || CrossesADirective(local, reference))
        {
            return null;
        }

        return new InlineEdit(local, reference, Inline(equalsValue.Value, reference).WithTriviaFrom(reference));
    }

    /// <summary>Returns whether a directive stands between the declaration and the read it folds into.</summary>
    /// <param name="local">The declaration being removed.</param>
    /// <param name="reference">The single read the value moves to.</param>
    /// <returns><see langword="true"/> when the inline would carry half a directive pair.</returns>
    private static bool CrossesADirective(LocalDeclarationStatementSyntax local, ExpressionSyntax reference)
    {
        SyntaxNode anchor = reference.FirstAncestorOrSelf<StatementSyntax>() is { } statement ? statement : reference;
        return DirectiveBoundaries.Separate(local, anchor);
    }

    /// <summary>Builds the expression that takes the read's place.</summary>
    /// <param name="value">The declaration's initializer.</param>
    /// <param name="reference">The read being replaced.</param>
    /// <returns>The initializer, parenthesized where the surrounding precedence needs it.</returns>
    private static ExpressionSyntax Inline(ExpressionSyntax value, ExpressionSyntax reference)
        => Sst2266InlineSingleUseLocalAnalyzer.NeedsParentheses(value, reference)
            ? SyntaxFactory.ParenthesizedExpression(value.WithoutTrivia())
            : value.WithoutTrivia();

    /// <summary>The declaration to remove, the reference to replace, and its inlined replacement.</summary>
    /// <param name="Declaration">The single-use local declaration being removed.</param>
    /// <param name="Reference">The one read being replaced.</param>
    /// <param name="Inlined">The initializer spliced into the read's place.</param>
    internal readonly record struct InlineEdit(
        LocalDeclarationStatementSyntax Declaration,
        IdentifierNameSyntax Reference,
        ExpressionSyntax Inlined);
}
