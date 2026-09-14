// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>Reads the identifier a written name ends in, so a syntactic prepass can match names before anything binds.</summary>
internal static class SyntaxNames
{
    /// <summary>Returns the rightmost identifier of a written name.</summary>
    /// <param name="name">The name as written: <c>Foo</c>, <c>Foo&lt;T&gt;</c>, <c>Ns.Foo</c> or <c>global::Foo</c>.</param>
    /// <returns>The rightmost identifier, without any type argument list.</returns>
    internal static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>Returns the rightmost identifier of a written type, when the type is written as a name.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns>The rightmost identifier, or <see langword="null"/> for a keyword, array, pointer, nullable or tuple type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string? GetSimpleName(TypeSyntax type) => type is NameSyntax name ? GetSimpleName(name) : null;

    /// <summary>Returns the rightmost identifier of a written type, when the outermost name is not generic.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns>
    /// The rightmost identifier of <c>Foo</c>, <c>Ns.Foo</c> or <c>global::Foo</c>, or <see langword="null"/> for an
    /// unqualified generic name and for any type that is not a name.
    /// </returns>
    internal static string? GetIdentifierName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => null,
    };
}
