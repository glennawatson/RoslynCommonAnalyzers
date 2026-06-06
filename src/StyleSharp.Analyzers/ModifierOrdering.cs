// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared, allocation-light helpers for the modifier-order rules (SST1206/SST1207). The
/// canonical order mirrors the repository's <c>csharp_preferred_modifier_order</c>: all
/// access modifiers first (their relative order is SST1207's concern), then <c>static</c>
/// and the remaining keywords, then <c>partial</c> last.
/// </summary>
internal static class ModifierOrdering
{
    /// <summary>The non-access modifier keywords in canonical order; the array index drives the rank.</summary>
    private static readonly SyntaxKind[] NonAccessOrder =
    [
        SyntaxKind.RequiredKeyword,
        SyntaxKind.ConstKeyword,
        SyntaxKind.StaticKeyword,
        SyntaxKind.ExternKeyword,
        SyntaxKind.NewKeyword,
        SyntaxKind.VirtualKeyword,
        SyntaxKind.AbstractKeyword,
        SyntaxKind.SealedKeyword,
        SyntaxKind.OverrideKeyword,
        SyntaxKind.ReadOnlyKeyword,
        SyntaxKind.UnsafeKeyword,
        SyntaxKind.VolatileKeyword,
        SyntaxKind.AsyncKeyword,
        SyntaxKind.PartialKeyword,
    ];

    /// <summary>The access modifier keywords in canonical order; the array index drives the sub-rank.</summary>
    private static readonly SyntaxKind[] AccessOrder =
    [
        SyntaxKind.PublicKeyword,
        SyntaxKind.FileKeyword,
        SyntaxKind.PrivateKeyword,
        SyntaxKind.ProtectedKeyword,
        SyntaxKind.InternalKeyword,
    ];

    /// <summary>Returns the modifier list of a node that carries modifiers, or an empty list.</summary>
    /// <param name="node">The declaration node.</param>
    /// <returns>The node's modifiers.</returns>
    public static SyntaxTokenList Modifiers(SyntaxNode node) => node switch
    {
        MemberDeclarationSyntax member => member.Modifiers,
        AccessorDeclarationSyntax accessor => accessor.Modifiers,
        LocalFunctionStatementSyntax local => local.Modifiers,
        _ => default,
    };

    /// <summary>Returns whether a modifier token is an access modifier.</summary>
    /// <param name="token">The modifier token.</param>
    /// <returns><see langword="true"/> when the token is an access modifier.</returns>
    public static bool IsAccess(SyntaxToken token) => Array.IndexOf(AccessOrder, token.Kind()) >= 0;

    /// <summary>Returns whether the modifier list declares any access modifier.</summary>
    /// <param name="modifiers">The modifier list.</param>
    /// <returns><see langword="true"/> when an access modifier is present.</returns>
    public static bool HasAccess(SyntaxTokenList modifiers)
    {
        foreach (var modifier in modifiers)
        {
            if (IsAccess(modifier))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the canonical rank of a modifier (access modifiers share rank 0).</summary>
    /// <param name="token">The modifier token.</param>
    /// <returns>The rank used to order modifiers.</returns>
    public static int Rank(SyntaxToken token)
    {
        if (IsAccess(token))
        {
            return 0;
        }

        var index = Array.IndexOf(NonAccessOrder, token.Kind());
        return index < 0 ? NonAccessOrder.Length : index + 1;
    }

    /// <summary>Returns the relative rank of an access modifier (private &lt; protected &lt; internal).</summary>
    /// <param name="token">The access modifier token.</param>
    /// <returns>The access sub-rank, or -1 when the token is not an access modifier.</returns>
    public static int AccessRank(SyntaxToken token) => Array.IndexOf(AccessOrder, token.Kind());
}
