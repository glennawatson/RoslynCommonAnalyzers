// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Rewrites a parameterless object creation as a read of a static member on the same type.</summary>
internal static class StaticMemberAccessRewrite
{
    /// <summary>Builds the static member read that replaces a creation.</summary>
    /// <param name="creation">The reported creation.</param>
    /// <param name="typeName">The simple type name written when the creation names no type, as a target-typed <c>new()</c> does.</param>
    /// <param name="memberName">The static member to read.</param>
    /// <returns>The member access, carrying the creation's trivia and reusing the type name the author wrote.</returns>
    internal static MemberAccessExpressionSyntax FromCreation(BaseObjectCreationExpressionSyntax creation, string typeName, string memberName)
    {
        var type = creation is ObjectCreationExpressionSyntax { Type: NameSyntax name }
            ? TypeNameExpression.From(name.WithoutTrivia())
            : SyntaxFactory.IdentifierName(typeName);

        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            type.WithLeadingTrivia(creation.GetLeadingTrivia()),
            SyntaxFactory.Token(SyntaxKind.DotToken),
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker),
                memberName,
                creation.GetTrailingTrivia())));
    }
}
