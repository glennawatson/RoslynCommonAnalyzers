// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>Resolves a reported single-character string literal and builds the char literal that replaces it.</summary>
internal static class SingleCharacterLiteralFix
{
    /// <summary>Resolves the single-character string literal a diagnostic was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="literal">The reported literal when found.</param>
    /// <returns><see langword="true"/> when the reported node is a single-character string literal.</returns>
    internal static bool TryFind(SyntaxNode root, Diagnostic diagnostic, out LiteralExpressionSyntax? literal)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is ExpressionSyntax expression
            && StringLiteralHelper.TryGetSingleCharacterLiteral(expression, out literal, out _))
        {
            return true;
        }

        literal = null;
        return false;
    }

    /// <summary>Builds the char literal for the string literal's one character, keeping its trivia.</summary>
    /// <param name="literal">A single-character string literal.</param>
    /// <returns>The char literal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LiteralExpressionSyntax ToCharacterLiteral(LiteralExpressionSyntax literal) =>
        SyntaxFactory.LiteralExpression(
            SyntaxKind.CharacterLiteralExpression,
            SyntaxFactory.Literal(
                literal.GetLeadingTrivia(),
                SymbolDisplay.FormatLiteral(literal.Token.ValueText[0], quote: true),
                literal.Token.ValueText[0],
                literal.GetTrailingTrivia()));
}
