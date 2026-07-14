// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Answers the one question a fix that inserts an <c>await</c> has to answer before it offers
/// itself: would the <c>await</c> compile where it is about to land? Being inside an <c>async</c>
/// function is not enough. C# forbids <c>await</c> in a <c>lock</c> body (CS1996), in an unsafe or
/// <c>fixed</c> context (CS4004), in a catch filter, and everywhere in a query expression except
/// the first <c>from</c> clause (CS1995) — and a fix that produces code which does not build is
/// worse than one that stays quiet, so the query case is refused wholesale rather than reasoned
/// about.
/// </summary>
internal static class AwaitPlacement
{
    /// <summary>Returns whether an <c>await</c> would be legal in the position a node occupies.</summary>
    /// <param name="node">The expression the await would replace.</param>
    /// <returns><see langword="true"/> when nothing between the node and its function forbids awaiting.</returns>
    public static bool IsLegalAt(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case LockStatementSyntax:
                case UnsafeStatementSyntax:
                case FixedStatementSyntax:
                case CatchFilterClauseSyntax:
                case QueryExpressionSyntax:
                    return false;
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case MemberDeclarationSyntax:
                    return true;
                default:
                    continue;
            }
        }

        return true;
    }
}
