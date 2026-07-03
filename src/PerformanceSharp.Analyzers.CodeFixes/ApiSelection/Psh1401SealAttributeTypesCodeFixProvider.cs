// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Adds the <c>sealed</c> modifier to an attribute class (PSH1401).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1401SealAttributeTypesCodeFixProvider))]
[Shared]
public sealed class Psh1401SealAttributeTypesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.SealAttributeTypes.Id);

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
                    "Seal the attribute type",
                    _ => Task.FromResult(Apply(context.Document, root, declaration)),
                    equivalenceKey: nameof(Psh1401SealAttributeTypesCodeFixProvider)),
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

        editor.ReplaceNode(declaration, AddSealedModifier(declaration));
    }

    /// <summary>Adds the <c>sealed</c> modifier to the reported class declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The class declaration to seal.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ClassDeclarationSyntax declaration)
        => document.WithSyntaxRoot(root.ReplaceNode(declaration, AddSealedModifier(declaration)));

    /// <summary>Inserts <c>sealed</c> after any accessibility modifiers, keeping modifier order valid.</summary>
    /// <param name="declaration">The class declaration to seal.</param>
    /// <returns>The sealed class declaration.</returns>
    private static ClassDeclarationSyntax AddSealedModifier(ClassDeclarationSyntax declaration)
    {
        var modifiers = declaration.Modifiers;
        var insertIndex = 0;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (IsAccessibilityModifier(modifiers[i].Kind()))
            {
                insertIndex = i + 1;
            }
        }

        var sealedToken = SyntaxFactory.Token(SyntaxKind.SealedKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        if (insertIndex > 0)
        {
            return declaration.WithModifiers(modifiers.Insert(insertIndex, sealedToken));
        }

        if (modifiers.Count == 0)
        {
            var keyword = declaration.Keyword;
            sealedToken = sealedToken.WithLeadingTrivia(keyword.LeadingTrivia);
            return declaration
                .WithKeyword(keyword.WithLeadingTrivia())
                .WithModifiers(SyntaxFactory.TokenList(sealedToken));
        }

        var first = modifiers[0];
        sealedToken = sealedToken.WithLeadingTrivia(first.LeadingTrivia);
        return declaration.WithModifiers(modifiers.Replace(first, first.WithLeadingTrivia()).Insert(0, sealedToken));
    }

    /// <summary>Returns whether a modifier kind is an accessibility modifier.</summary>
    /// <param name="kind">The modifier kind to inspect.</param>
    /// <returns><see langword="true"/> for accessibility modifiers.</returns>
    private static bool IsAccessibilityModifier(SyntaxKind kind)
        => kind is SyntaxKind.PublicKeyword
            or SyntaxKind.PrivateKeyword
            or SyntaxKind.ProtectedKeyword
            or SyntaxKind.InternalKeyword
            or SyntaxKind.FileKeyword;
}
