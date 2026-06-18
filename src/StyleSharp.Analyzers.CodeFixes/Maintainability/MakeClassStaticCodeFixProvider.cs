// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Adds the <c>static</c> modifier to a class whose members are all static (SST1432).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeClassStaticCodeFixProvider))]
[Shared]
public sealed class MakeClassStaticCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.MakeClassStatic.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ClassDeclarationSyntax>() is not { } declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Mark the class 'static'",
                    _ => Task.FromResult(Apply(context.Document, root, declaration)),
                    equivalenceKey: nameof(MakeClassStaticCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ClassDeclarationSyntax>() is not { } declaration)
        {
            return;
        }

        editor.ReplaceNode(declaration, (current, _) => MakeStatic((ClassDeclarationSyntax)current));
    }

    /// <summary>Inserts <c>static</c> after the access modifiers, moving leading trivia when the list is empty.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The class declaration to mark static.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ClassDeclarationSyntax declaration)
        => document.WithSyntaxRoot(root.ReplaceNode(declaration, MakeStatic(declaration)));

    /// <summary>Builds the class declaration with <c>static</c> inserted after the access modifiers.</summary>
    /// <param name="declaration">The class declaration to mark static.</param>
    /// <returns>The rewritten declaration.</returns>
    private static ClassDeclarationSyntax MakeStatic(ClassDeclarationSyntax declaration)
    {
        if (declaration.Modifiers.Count == 0)
        {
            // No modifiers: move the declaration's leading trivia onto 'static' and re-indent the keyword.
            var staticToken = SyntaxFactory.Token(declaration.GetLeadingTrivia(), SyntaxKind.StaticKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
            return declaration
                .WithKeyword(declaration.Keyword.WithLeadingTrivia(SyntaxFactory.TriviaList()))
                .WithModifiers(SyntaxFactory.TokenList(staticToken));
        }

        var appended = SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        return declaration.WithModifiers(declaration.Modifiers.Add(appended));
    }
}
