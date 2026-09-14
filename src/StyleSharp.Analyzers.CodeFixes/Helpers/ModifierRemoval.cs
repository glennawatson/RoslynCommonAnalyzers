// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Removes one modifier from a member declaration without losing the declaration's indentation.</summary>
/// <remarks>
/// The first token of an unattributed declaration carries its leading trivia, so removing the first modifier hands
/// that trivia to the next modifier, or to the first child after the modifier list when none are left.
/// </remarks>
internal static class ModifierRemoval
{
    /// <summary>Removes the modifier of one kind, if the declaration still has it.</summary>
    /// <param name="declaration">The member declaration.</param>
    /// <param name="kind">The modifier kind to remove.</param>
    /// <returns>The declaration without the modifier, or <paramref name="declaration"/> when it has none of that kind.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MemberDeclarationSyntax RemoveKind(MemberDeclarationSyntax declaration, SyntaxKind kind) =>
        RemoveAt(declaration, declaration.Modifiers.IndexOf(kind));

    /// <summary>Removes the modifier at a position in the declaration's modifier list.</summary>
    /// <param name="declaration">The member declaration.</param>
    /// <param name="modifierIndex">The modifier's index, or -1 when it is absent.</param>
    /// <returns>The declaration without the modifier, or <paramref name="declaration"/> when the index is negative.</returns>
    internal static MemberDeclarationSyntax RemoveAt(MemberDeclarationSyntax declaration, int modifierIndex)
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
        TypeDeclarationRewrite.WithHead(declaration, modifiers, declaration.Keyword.WithLeadingTrivia(leading))
            ?? declaration.WithModifiers(modifiers).WithLeadingTrivia(leading);

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
