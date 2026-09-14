// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rebuilds a class, struct, interface or record declaration in a single node update.</summary>
/// <remarks>
/// These kinds share every child the methods replace but expose no common full update, so each method switches on
/// the kind once. A kind a method does not list returns <see langword="null"/> for the caller to handle.
/// </remarks>
internal static class TypeDeclarationRewrite
{
    /// <summary>Replaces a type declaration's modifiers and keyword.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="modifiers">The modifiers to write.</param>
    /// <param name="keyword">The <c>class</c>, <c>struct</c>, <c>interface</c> or <c>record</c> keyword to write.</param>
    /// <returns>The rebuilt declaration, or <see langword="null"/> for any other kind of type declaration.</returns>
    internal static TypeDeclarationSyntax? WithHead(TypeDeclarationSyntax declaration, in SyntaxTokenList modifiers, in SyntaxToken keyword) =>
        declaration switch
        {
            ClassDeclarationSyntax type => type.Update(
                type.AttributeLists,
                modifiers,
                keyword,
                type.Identifier,
                type.TypeParameterList,
                type.ParameterList,
                type.BaseList,
                type.ConstraintClauses,
                type.OpenBraceToken,
                type.Members,
                type.CloseBraceToken,
                type.SemicolonToken),
            StructDeclarationSyntax type => type.Update(
                type.AttributeLists,
                modifiers,
                keyword,
                type.Identifier,
                type.TypeParameterList,
                type.ParameterList,
                type.BaseList,
                type.ConstraintClauses,
                type.OpenBraceToken,
                type.Members,
                type.CloseBraceToken,
                type.SemicolonToken),
            InterfaceDeclarationSyntax type => type.Update(
                type.AttributeLists,
                modifiers,
                keyword,
                type.Identifier,
                type.TypeParameterList,
                type.ParameterList,
                type.BaseList,
                type.ConstraintClauses,
                type.OpenBraceToken,
                type.Members,
                type.CloseBraceToken,
                type.SemicolonToken),
            RecordDeclarationSyntax type => type.Update(
                type.AttributeLists,
                modifiers,
                keyword,
                type.ClassOrStructKeyword,
                type.Identifier,
                type.TypeParameterList,
                type.ParameterList,
                type.BaseList,
                type.ConstraintClauses,
                type.OpenBraceToken,
                type.Members,
                type.CloseBraceToken,
                type.SemicolonToken),
            _ => null,
        };

    /// <summary>Replaces a class, struct or interface declaration's primary-constructor parameters, base list, members and closing tokens.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="parameterList">The primary-constructor parameter list to write.</param>
    /// <param name="baseList">The base list to write.</param>
    /// <param name="members">The members to write.</param>
    /// <param name="closeBraceToken">The closing brace to write.</param>
    /// <param name="semicolonToken">The trailing semicolon to write.</param>
    /// <returns>The rebuilt declaration, or <see langword="null"/> for a record or any other kind of type declaration.</returns>
    internal static TypeDeclarationSyntax? WithBody(
        TypeDeclarationSyntax declaration,
        ParameterListSyntax? parameterList,
        BaseListSyntax? baseList,
        SyntaxList<MemberDeclarationSyntax> members,
        in SyntaxToken closeBraceToken,
        in SyntaxToken semicolonToken) =>
        declaration switch
        {
            ClassDeclarationSyntax type => type.Update(
                type.AttributeLists,
                type.Modifiers,
                type.Keyword,
                type.Identifier,
                type.TypeParameterList,
                parameterList,
                baseList,
                type.ConstraintClauses,
                type.OpenBraceToken,
                members,
                closeBraceToken,
                semicolonToken),
            StructDeclarationSyntax type => type.Update(
                type.AttributeLists,
                type.Modifiers,
                type.Keyword,
                type.Identifier,
                type.TypeParameterList,
                parameterList,
                baseList,
                type.ConstraintClauses,
                type.OpenBraceToken,
                members,
                closeBraceToken,
                semicolonToken),
            InterfaceDeclarationSyntax type => type.Update(
                type.AttributeLists,
                type.Modifiers,
                type.Keyword,
                type.Identifier,
                type.TypeParameterList,
                parameterList,
                baseList,
                type.ConstraintClauses,
                type.OpenBraceToken,
                members,
                closeBraceToken,
                semicolonToken),
            _ => null,
        };
}
