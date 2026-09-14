// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Recognizes the spellings of <c>System.Object</c> that syntax alone can vouch for.</summary>
internal static class ObjectTypeSyntax
{
    /// <summary>Returns whether a type syntax unambiguously denotes <c>System.Object</c> without semantic binding.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns><see langword="true"/> for <c>object</c>, <c>System.Object</c> and <c>global::System.Object</c>.</returns>
    /// <remarks>A bare <c>Object</c> is not accepted: a type of that name in scope could shadow the framework's.</remarks>
    internal static bool IsUnambiguousObjectType(TypeSyntax type) =>
        (type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword))
            || (type is QualifiedNameSyntax { Right.Identifier.ValueText: "Object", Left: var left } && IsSystemNamespace(left));

    /// <summary>Returns whether a name syntax denotes the <c>System</c> namespace.</summary>
    /// <param name="name">The syntax to inspect.</param>
    /// <returns><see langword="true"/> for <c>System</c> and <c>global::System</c>.</returns>
    internal static bool IsSystemNamespace(NameSyntax name) =>
        name is IdentifierNameSyntax { Identifier.ValueText: "System" }
            or AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "global", Name.Identifier.ValueText: "System" };
}
