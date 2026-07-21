// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds <c>static</c> to a partial class part that omits it while another part declares it (SST2335), so each
/// part states the type's static-ness on its own.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2335PartialStaticMismatchCodeFixProvider))]
[Shared]
public sealed class Sst2335PartialStaticMismatchCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DesignRules.PartialTypeStaticModifierMismatch.Id);

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
                    "Add 'static'",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, MakeStatic(declaration)))),
                    equivalenceKey: nameof(Sst2335PartialStaticMismatchCodeFixProvider)),
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

        editor.ReplaceNode(declaration, (current, _) => MakeStatic((ClassDeclarationSyntax)current));
    }

    /// <summary>Resolves the diagnostic's span to the class part it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The class declaration, or <see langword="null"/> when the shape no longer matches.</returns>
    private static ClassDeclarationSyntax? FindDeclaration(SyntaxNode root, Diagnostic diagnostic)
        => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<ClassDeclarationSyntax>();

    /// <summary>Builds the class declaration with <c>static</c> inserted before <c>partial</c>.</summary>
    /// <param name="declaration">The class part to make static.</param>
    /// <returns>The rewritten declaration.</returns>
    private static ClassDeclarationSyntax MakeStatic(ClassDeclarationSyntax declaration)
    {
        var modifiers = declaration.Modifiers;
        if (modifiers.Count == 0)
        {
            var lone = SyntaxFactory.Token(declaration.GetLeadingTrivia(), SyntaxKind.StaticKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
            return declaration
                .WithKeyword(declaration.Keyword.WithLeadingTrivia(SyntaxFactory.TriviaList()))
                .WithModifiers(SyntaxFactory.TokenList(lone));
        }

        var partialIndex = modifiers.IndexOf(SyntaxKind.PartialKeyword);
        if (partialIndex < 0)
        {
            var appended = SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space);
            return declaration.WithModifiers(modifiers.Add(appended));
        }

        // 'partial' stays last, so 'static' goes in front of it — and takes over its leading trivia, which is
        // the declaration's own indentation whenever 'partial' is the first modifier.
        var partial = modifiers[partialIndex];
        var inserted = SyntaxFactory.Token(partial.LeadingTrivia, SyntaxKind.StaticKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        var reindented = modifiers.Replace(partial, partial.WithLeadingTrivia(SyntaxFactory.TriviaList()));
        return declaration.WithModifiers(reindented.Insert(partialIndex, inserted));
    }
}
