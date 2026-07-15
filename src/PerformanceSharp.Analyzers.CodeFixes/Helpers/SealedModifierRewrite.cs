// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Inserts the <c>sealed</c> modifier into a class declaration, shared by the sealing fixes
/// (PSH1401, PSH1411). The modifier goes after <c>public</c>/<c>internal</c>/<c>private</c>/
/// <c>file</c> and before everything else — notably before <c>partial</c>, which must stay adjacent
/// to the <c>class</c> keyword. When there are no modifiers at all, <c>sealed</c> takes over the
/// <c>class</c> keyword's leading trivia so the declaration's indentation and any doc comment stay
/// put.
/// </summary>
internal static class SealedModifierRewrite
{
    /// <summary>Inserts <c>sealed</c> after any accessibility modifiers, keeping modifier order valid.</summary>
    /// <param name="declaration">The class declaration to seal.</param>
    /// <returns>The sealed class declaration.</returns>
    public static ClassDeclarationSyntax AddSealed(ClassDeclarationSyntax declaration)
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
