// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Seals a class that implements <c>IEquatable&lt;T&gt;</c> against itself (SST2301).
/// </summary>
/// <remarks>
/// The fix says what the type already meant: equality is decided here and nowhere else. If the class is
/// already derived from — in this project or another — sealing it will not compile, and that failure is
/// the honest one: the hierarchy and the contract were never compatible, and the answer is to move
/// equality somewhere a hierarchy can keep it, not to leave the type open and the equality asymmetric.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2301SealEquatableTypeCodeFixProvider))]
[Shared]
public sealed class Sst2301SealEquatableTypeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.EquatableTypeShouldBeSealed.Id);

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
            if (FindDeclaration(root, diagnostic) is not { } declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Seal the type",
                    _ => Task.FromResult(Apply(context.Document, root, declaration)),
                    equivalenceKey: nameof(Sst2301SealEquatableTypeCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (FindDeclaration(editor.OriginalRoot, diagnostic) is not { } declaration)
        {
            return;
        }

        editor.ReplaceNode(declaration, (current, _) => MakeSealed((ClassDeclarationSyntax)current));
    }

    /// <summary>Applies the fix for one unsealed equatable class.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The class declaration to seal.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ClassDeclarationSyntax declaration)
        => document.WithSyntaxRoot(root.ReplaceNode(declaration, MakeSealed(declaration)));

    /// <summary>Resolves the diagnostic's span to the class it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The class declaration, or <see langword="null"/> when the shape no longer matches.</returns>
    private static ClassDeclarationSyntax? FindDeclaration(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ClassDeclarationSyntax>();

    /// <summary>Builds the class declaration with <c>sealed</c> inserted after the access modifiers.</summary>
    /// <param name="declaration">The class declaration to seal.</param>
    /// <returns>The rewritten declaration.</returns>
    private static ClassDeclarationSyntax MakeSealed(ClassDeclarationSyntax declaration)
    {
        var modifiers = declaration.Modifiers;
        if (modifiers.Count == 0)
        {
            // No modifiers: move the declaration's leading trivia onto 'sealed' and re-indent the keyword.
            var lone = SyntaxFactory.Token(declaration.GetLeadingTrivia(), SyntaxKind.SealedKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
            return declaration
                .WithKeyword(declaration.Keyword.WithLeadingTrivia(SyntaxFactory.TriviaList()))
                .WithModifiers(SyntaxFactory.TokenList(lone));
        }

        var partialIndex = modifiers.IndexOf(SyntaxKind.PartialKeyword);
        if (partialIndex < 0)
        {
            var appended = SyntaxFactory.Token(SyntaxKind.SealedKeyword).WithTrailingTrivia(SyntaxFactory.Space);
            return declaration.WithModifiers(modifiers.Add(appended));
        }

        // 'partial' stays last in the list, so 'sealed' goes in front of it — and takes over its leading
        // trivia, which is the declaration's own indentation whenever 'partial' is the first modifier.
        var partial = modifiers[partialIndex];
        var inserted = SyntaxFactory.Token(partial.LeadingTrivia, SyntaxKind.SealedKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        var reindented = modifiers.Replace(partial, partial.WithLeadingTrivia(SyntaxFactory.TriviaList()));
        return declaration.WithModifiers(reindented.Insert(partialIndex, inserted));
    }
}
