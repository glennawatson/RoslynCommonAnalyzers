// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>
/// Adds a base type to a type declaration on the same line as its name, so an interface a code fix
/// introduces reads as <c>class C : IFoo</c> rather than being pushed onto a line of its own.
/// </summary>
internal static class BaseListInsertion
{
    /// <summary>Adds a base type to a type declaration, keeping it inline with the type header.</summary>
    /// <param name="type">The type declaration.</param>
    /// <param name="baseType">The base type to add.</param>
    /// <returns>The updated declaration.</returns>
    public static TypeDeclarationSyntax AddBaseType(TypeDeclarationSyntax type, BaseTypeSyntax baseType)
    {
        if (type.BaseList is { } existing)
        {
            return type.WithBaseList(existing.AddTypes(baseType));
        }

        // A type with no base list carries the newline before its '{' as the header token's trailing
        // trivia; move it onto the new base list so the interface sits inline and the brace stays put.
        var anchor = HeaderToken(type);
        var baseList = SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList(baseType))
            .WithTrailingTrivia(anchor.TrailingTrivia);
        return type
            .ReplaceToken(anchor, anchor.WithTrailingTrivia(SyntaxFactory.Space))
            .WithBaseList(baseList);
    }

    /// <summary>Gets the token the base list follows: the primary-constructor parameters, the type parameters, or the name.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The header token.</returns>
    private static SyntaxToken HeaderToken(TypeDeclarationSyntax type)
    {
        if (type.ParameterList is { } parameters)
        {
            return parameters.CloseParenToken;
        }

        return type.TypeParameterList is { } typeParameters ? typeParameters.GreaterThanToken : type.Identifier;
    }
}
