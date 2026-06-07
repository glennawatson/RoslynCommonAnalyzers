// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Computes whether a declaration forms part of the externally visible API
/// surface, using only syntax (modifiers + containing types) so no semantic model
/// binding is needed. "Externally visible" means the declaration and every
/// containing type are <c>public</c> or <c>protected</c> (interface and enum
/// members inherit their container's visibility).
/// </summary>
internal static class DocumentationVisibility
{
    /// <summary>Returns whether <paramref name="member"/> is externally visible.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is part of the public API surface.</returns>
    public static bool IsExposed(SyntaxNode member)
    {
        if (!IsMemberLevelExposed(member))
        {
            return false;
        }

        for (var parent = member.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case TypeDeclarationSyntax type when !ExposesMembers(type.Modifiers):
                case EnumDeclarationSyntax @enum when !ExposesMembers(@enum.Modifiers):
                    return false;
                case BaseNamespaceDeclarationSyntax:
                case CompilationUnitSyntax:
                    return true;
            }
        }

        return true;
    }

    /// <summary>Returns whether a modifier list exposes the element (<c>public</c> or <c>protected</c>).</summary>
    /// <param name="modifiers">The modifiers.</param>
    /// <returns><see langword="true"/> when exposed.</returns>
    private static bool ExposesMembers(SyntaxTokenList modifiers)
        => ModifierListHelper.ContainsEither(modifiers, SyntaxKind.PublicKeyword, SyntaxKind.ProtectedKeyword);

    /// <summary>Returns whether the member's own modifiers expose it (accounting for interface/enum defaults).</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member itself is exposed.</returns>
    private static bool IsMemberLevelExposed(SyntaxNode member)
    {
        if (member is EnumMemberDeclarationSyntax)
        {
            return true;
        }

        if (member is not MemberDeclarationSyntax declaration)
        {
            return false;
        }

        // Interface members are implicitly public unless explicitly private.
        return member.Parent is InterfaceDeclarationSyntax
            ? !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.PrivateKeyword)
            : ExposesMembers(declaration.Modifiers);
    }
}
