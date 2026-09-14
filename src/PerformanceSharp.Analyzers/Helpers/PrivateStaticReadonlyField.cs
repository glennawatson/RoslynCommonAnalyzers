// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Syntax checks for a private static readonly field whose every use a rule scans before suggesting a
/// cheaper shape (PSH1013, PSH1114): the declaration shape, the containing type whose body is the whole
/// scope the field can be used from, and the recognition of one token as a use of the field.
/// </summary>
internal static class PrivateStaticReadonlyField
{
    /// <summary>Returns whether a field declares exactly one variable and gives it an initializer.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> for a single initialized declarator.</returns>
    internal static bool IsSingleInitializedVariable(FieldDeclarationSyntax field) =>
        field.Declaration.Variables.Count == 1
            && field.Declaration.Variables[0].Initializer is not null;

    /// <summary>Returns whether a field is private (explicitly or by default), static, and readonly.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> when the modifier shape matches.</returns>
    internal static bool HasPrivateStaticReadonlyModifiers(FieldDeclarationSyntax field)
    {
        var modifiers = field.Modifiers;
        if (!modifiers.Any(SyntaxKind.StaticKeyword) || !modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return false;
        }

        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword or SyntaxKind.ProtectedKeyword)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the type declaring a field when that declaration is the only part of the type.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="containingType">The declaring type, when it is not partial.</param>
    /// <returns><see langword="true"/> when the declaring type's body holds every use of the field.</returns>
    /// <remarks>Another part of a partial type could read, mutate, or leak the field out of sight of the scan.</remarks>
    internal static bool TryGetNonPartialContainingType(FieldDeclarationSyntax field, [NotNullWhen(true)] out TypeDeclarationSyntax? containingType)
    {
        if (field.Parent is TypeDeclarationSyntax parent && !parent.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            containingType = parent;
            return true;
        }

        containingType = null;
        return false;
    }

    /// <summary>Returns whether a token names the field somewhere other than its own declarator.</summary>
    /// <param name="token">The token to inspect.</param>
    /// <param name="name">The field name.</param>
    /// <param name="declaratorStart">The position of the declarator's identifier, which is not a use.</param>
    /// <param name="identifier">The identifier name the token belongs to.</param>
    /// <returns><see langword="true"/> for an identifier name spelling the field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetReference(in SyntaxToken token, string name, int declaratorStart, [NotNullWhen(true)] out IdentifierNameSyntax? identifier)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken)
            || token.SpanStart == declaratorStart
            || token.ValueText != name
            || token.Parent is not IdentifierNameSyntax reference)
        {
            identifier = null;
            return false;
        }

        identifier = reference;
        return true;
    }

    /// <summary>Returns the node a reference to the field is used as.</summary>
    /// <param name="identifier">The identifier naming the field.</param>
    /// <returns>The whole <c>Type.Field</c> access for a qualified reference, otherwise the identifier itself.</returns>
    internal static SyntaxNode GetUsage(IdentifierNameSyntax identifier) =>
        identifier.Parent is MemberAccessExpressionSyntax qualification && qualification.Name == identifier
            ? qualification
            : identifier;
}
