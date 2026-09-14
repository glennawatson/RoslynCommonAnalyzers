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

    /// <summary>Returns the rightmost identifier of an expression written as a name or a member access.</summary>
    /// <param name="expression">The expression as written: <c>Foo</c>, <c>a.b.Foo</c> or <c>global::Foo</c>.</param>
    /// <returns>The rightmost identifier, or <see langword="null"/> for an unqualified generic name and any other expression.</returns>
    internal static string? GetMemberName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether any attribute in the lists is written with a simple name the predicate accepts.</summary>
    /// <param name="attributeLists">The attribute lists to inspect.</param>
    /// <param name="isName">Returns whether an attribute's simple name matches.</param>
    /// <returns><see langword="true"/> when an attribute's simple name matches.</returns>
    internal static bool AnyAttributeNamed(SyntaxList<AttributeListSyntax> attributeLists, Func<string, bool> isName)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (isName(GetSimpleName(attributes[j].Name)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
