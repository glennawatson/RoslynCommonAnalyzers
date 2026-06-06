// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Classifies a field's modifiers syntactically (no semantic model) so the field
/// naming analyzer can pick the right convention in a single pass.
/// </summary>
/// <param name="IsConst">Whether the field is <see langword="const"/>.</param>
/// <param name="IsStatic">Whether the field is <see langword="static"/>.</param>
/// <param name="IsReadOnly">Whether the field is <see langword="readonly"/>.</param>
/// <param name="IsPrivate">Whether the field is effectively private (including <c>private protected</c> and the implicit default).</param>
internal readonly record struct FieldClassification(bool IsConst, bool IsStatic, bool IsReadOnly, bool IsPrivate)
{
    /// <summary>Classifies a field from its modifier list.</summary>
    /// <param name="modifiers">The field declaration's modifiers.</param>
    /// <returns>The classification.</returns>
    public static FieldClassification Classify(SyntaxTokenList modifiers)
    {
        // `private protected` keeps both keywords but is treated as private; a
        // field with no access modifier is private by default.
        var hasOtherAccess = modifiers.Any(SyntaxKind.PublicKeyword)
            || modifiers.Any(SyntaxKind.InternalKeyword)
            || modifiers.Any(SyntaxKind.ProtectedKeyword);
        var isPrivate = modifiers.Any(SyntaxKind.PrivateKeyword) || !hasOtherAccess;

        return new(
            modifiers.Any(SyntaxKind.ConstKeyword),
            modifiers.Any(SyntaxKind.StaticKeyword),
            modifiers.Any(SyntaxKind.ReadOnlyKeyword),
            isPrivate);
    }
}
