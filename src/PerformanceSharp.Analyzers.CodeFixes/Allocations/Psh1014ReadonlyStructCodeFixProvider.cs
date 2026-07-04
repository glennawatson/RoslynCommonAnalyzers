// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds the <c>readonly</c> modifier to a reported struct declaration (PSH1014). The modifier
/// lands after any access modifiers and before a <c>ref</c> modifier, matching the standard
/// ordering, and the declaration's leading trivia moves onto it when it becomes the first
/// token.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1014ReadonlyStructCodeFixProvider))]
[Shared]
public sealed class Psh1014ReadonlyStructCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.MakeStructReadonly.Id);

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
            if (TryGetStruct(root, diagnostic) is not { } declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make the struct readonly",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, AddReadonlyModifier(declaration)))),
                    equivalenceKey: nameof(Psh1014ReadonlyStructCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetStruct(editor.OriginalRoot, diagnostic) is not { } declaration)
        {
            return;
        }

        editor.ReplaceNode(declaration, AddReadonlyModifier(declaration));
    }

    /// <summary>Builds the declaration with a readonly modifier in standard position.</summary>
    /// <param name="declaration">The struct declaration to rewrite.</param>
    /// <returns>The readonly declaration.</returns>
    internal static TypeDeclarationSyntax AddReadonlyModifier(TypeDeclarationSyntax declaration)
    {
        var readonlyToken = SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        var modifiers = declaration.Modifiers;
        if (modifiers.Count == 0)
        {
            var keyword = declaration.Keyword;
            return declaration
                .WithModifiers(SyntaxFactory.TokenList(readonlyToken.WithLeadingTrivia(keyword.LeadingTrivia)))
                .WithKeyword(keyword.WithLeadingTrivia());
        }

        var insertIndex = modifiers.Count;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.RefKeyword) || modifiers[i].IsKind(SyntaxKind.UnsafeKeyword))
            {
                insertIndex = i;
                break;
            }
        }

        if (insertIndex == 0)
        {
            var first = modifiers[0];
            return declaration.WithModifiers(
                modifiers.Replace(first, first.WithLeadingTrivia())
                    .Insert(0, readonlyToken.WithLeadingTrivia(first.LeadingTrivia)));
        }

        return declaration.WithModifiers(modifiers.Insert(insertIndex, readonlyToken));
    }

    /// <summary>Returns the reported struct declaration when it still lacks the readonly modifier.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The declaration, or <see langword="null"/> when the shape no longer matches.</returns>
    private static TypeDeclarationSyntax? TryGetStruct(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is TypeDeclarationSyntax { RawKind: (int)SyntaxKind.StructDeclaration or (int)SyntaxKind.RecordStructDeclaration } declaration
            && !declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            ? declaration
            : null;
}
