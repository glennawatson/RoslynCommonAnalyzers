// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Removes the modifier reported by SST1419 (redundant) or SST1427 (<c>protected</c> in a sealed type).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveModifierCodeFixProvider))]
[Shared]
public sealed class RemoveModifierCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.NoRedundantModifier.Id,
        MaintainabilityRules.NoProtectedInSealed.Id);

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

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } declaration
                || declaration.Modifiers.IndexOf(token) < 0)
            {
                // A redundant 'checked'/'unchecked' context (SST1419) is not a member modifier; removing it
                // is a structural rewrite, not a token deletion, so no fix is offered for that shape.
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove redundant modifier",
                    cancellationToken => Task.FromResult(RemoveModifier(context.Document, root, declaration, token)),
                    equivalenceKey: nameof(RemoveModifierCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var token = editor.OriginalRoot.FindToken(diagnostic.Location.SourceSpan.Start);
        if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } declaration)
        {
            return;
        }

        // Remove by index, computed lazily against the current (tracked) node so a parent edit applied
        // first keeps the descendant's annotations — see BatchEditFixAllProvider. The token's absolute
        // span shifts under earlier edits, so we cannot match it directly at apply time.
        var modifierIndex = declaration.Modifiers.IndexOf(token);
        if (modifierIndex < 0)
        {
            return;
        }

        // Removing the first modifier would otherwise drop the declaration's leading indentation, so
        // carry it over. For a non-leading modifier this is a no-op.
        editor.ReplaceNode(declaration, (current, _) => RemoveModifier((MemberDeclarationSyntax)current, modifierIndex));
    }

    /// <summary>Removes the reported modifier token from the declaration.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The current syntax root.</param>
    /// <param name="declaration">The declaration that owns the redundant modifier.</param>
    /// <param name="token">The modifier token to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Document RemoveModifier(Document document, SyntaxNode root, MemberDeclarationSyntax declaration, SyntaxToken token)
    {
        // Removing the first modifier would otherwise drop the declaration's leading indentation, so
        // carry it over. For a non-leading modifier this is a no-op.
        var updated = RemoveModifier(declaration, declaration.Modifiers.IndexOf(token));
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated));
    }

    /// <summary>Removes a modifier and transfers leading trivia in the same declaration update.</summary>
    /// <param name="declaration">The declaration to update.</param>
    /// <param name="modifierIndex">The modifier's index, or -1 when it is absent.</param>
    /// <returns>The declaration with the modifier removed.</returns>
    private static MemberDeclarationSyntax RemoveModifier(MemberDeclarationSyntax declaration, int modifierIndex)
    {
        if (modifierIndex < 0)
        {
            return declaration;
        }

        var modifiers = declaration.Modifiers.RemoveAt(modifierIndex);
        if (modifierIndex > 0 || declaration.AttributeLists.Count > 0)
        {
            return declaration.WithModifiers(modifiers);
        }

        var leading = declaration.GetLeadingTrivia();
        if (modifiers.Count > 0)
        {
            var first = modifiers[0];
            return declaration.WithModifiers(modifiers.Replace(first, first.WithLeadingTrivia(leading)));
        }

        return declaration switch
        {
            TypeDeclarationSyntax type => RemoveTypeModifier(type, modifiers, leading),
            BaseMethodDeclarationSyntax method => RemoveMethodModifier(method, modifiers, leading),
            _ => RemoveMemberModifier(declaration, modifiers, leading),
        };
    }

    /// <summary>Updates a type's modifiers and keyword trivia together.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="modifiers">The remaining modifiers.</param>
    /// <param name="leading">The declaration's leading trivia.</param>
    /// <returns>The updated declaration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeDeclarationSyntax RemoveTypeModifier(TypeDeclarationSyntax declaration, in SyntaxTokenList modifiers, in SyntaxTriviaList leading) =>
        declaration switch
        {
            ClassDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.Keyword.WithLeadingTrivia(leading),
                member.Identifier,
                member.TypeParameterList,
                member.ParameterList,
                member.BaseList,
                member.ConstraintClauses,
                member.OpenBraceToken,
                member.Members,
                member.CloseBraceToken,
                member.SemicolonToken),
            StructDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.Keyword.WithLeadingTrivia(leading),
                member.Identifier,
                member.TypeParameterList,
                member.ParameterList,
                member.BaseList,
                member.ConstraintClauses,
                member.OpenBraceToken,
                member.Members,
                member.CloseBraceToken,
                member.SemicolonToken),
            InterfaceDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.Keyword.WithLeadingTrivia(leading),
                member.Identifier,
                member.TypeParameterList,
                member.ParameterList,
                member.BaseList,
                member.ConstraintClauses,
                member.OpenBraceToken,
                member.Members,
                member.CloseBraceToken,
                member.SemicolonToken),
            RecordDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.Keyword.WithLeadingTrivia(leading),
                member.ClassOrStructKeyword,
                member.Identifier,
                member.TypeParameterList,
                member.ParameterList,
                member.BaseList,
                member.ConstraintClauses,
                member.OpenBraceToken,
                member.Members,
                member.CloseBraceToken,
                member.SemicolonToken),
            _ => declaration.WithModifiers(modifiers).WithLeadingTrivia(leading),
        };

    /// <summary>Updates a method's modifiers and first child trivia together.</summary>
    /// <param name="declaration">The method declaration.</param>
    /// <param name="modifiers">The remaining modifiers.</param>
    /// <param name="leading">The declaration's leading trivia.</param>
    /// <returns>The updated declaration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BaseMethodDeclarationSyntax RemoveMethodModifier(BaseMethodDeclarationSyntax declaration, in SyntaxTokenList modifiers, in SyntaxTriviaList leading) =>
        declaration switch
        {
            MethodDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.ReturnType.WithLeadingTrivia(leading),
                member.ExplicitInterfaceSpecifier,
                member.Identifier,
                member.TypeParameterList,
                member.ParameterList,
                member.ConstraintClauses,
                member.Body,
                member.ExpressionBody,
                member.SemicolonToken),
            ConstructorDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.Identifier.WithLeadingTrivia(leading),
                member.ParameterList,
                member.Initializer,
                member.Body,
                member.ExpressionBody,
                member.SemicolonToken),
            _ => declaration.WithModifiers(modifiers).WithLeadingTrivia(leading),
        };

    /// <summary>Updates a property's, event's, or field's modifiers and first child trivia together.</summary>
    /// <param name="declaration">The member declaration.</param>
    /// <param name="modifiers">The remaining modifiers.</param>
    /// <param name="leading">The declaration's leading trivia.</param>
    /// <returns>The updated declaration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MemberDeclarationSyntax RemoveMemberModifier(MemberDeclarationSyntax declaration, in SyntaxTokenList modifiers, in SyntaxTriviaList leading) =>
        declaration switch
        {
            PropertyDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.Type.WithLeadingTrivia(leading),
                member.ExplicitInterfaceSpecifier,
                member.Identifier,
                member.AccessorList,
                member.ExpressionBody,
                member.Initializer,
                member.SemicolonToken),
            IndexerDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.Type.WithLeadingTrivia(leading),
                member.ExplicitInterfaceSpecifier,
                member.ThisKeyword,
                member.ParameterList,
                member.AccessorList,
                member.ExpressionBody,
                member.SemicolonToken),
            EventDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.EventKeyword.WithLeadingTrivia(leading),
                member.Type,
                member.ExplicitInterfaceSpecifier,
                member.Identifier,
                member.AccessorList,
                member.SemicolonToken),
            FieldDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.Declaration.WithLeadingTrivia(leading),
                member.SemicolonToken),
            EventFieldDeclarationSyntax member => member.Update(
                member.AttributeLists,
                modifiers,
                member.EventKeyword.WithLeadingTrivia(leading),
                member.Declaration,
                member.SemicolonToken),
            _ => declaration.WithModifiers(modifiers).WithLeadingTrivia(leading),
        };
}
