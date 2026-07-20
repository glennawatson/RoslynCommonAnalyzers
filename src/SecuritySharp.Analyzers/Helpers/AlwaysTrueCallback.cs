// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Decides, without following any value that flows in from elsewhere, whether a callback expression
/// unconditionally yields the literal <see langword="true"/>: an expression lambda <c>_ =&gt; true</c>, a block
/// lambda or anonymous method whose only reachable result is <c>return true;</c>, or a method group to a source
/// method of that shape. Shared by the rules that flag an always-permissive callback — a CORS origin predicate
/// (SES1502) and a server-certificate validation callback (SES1108) — where a body that always returns true
/// silently disables the check it was meant to perform.
/// </summary>
internal static class AlwaysTrueCallback
{
    /// <summary>Returns whether a lambda or anonymous method always yields <see langword="true"/>.</summary>
    /// <param name="function">The lambda or anonymous method.</param>
    /// <returns><see langword="true"/> when the body is <c>=&gt; true</c> or a block whose only result is <c>return true;</c>.</returns>
    internal static bool IsAlwaysTrueLambda(AnonymousFunctionExpressionSyntax function)
        => IsAlwaysTrueBody(function.ExpressionBody, function.Block);

    /// <summary>Returns whether the method a method group references always yields <see langword="true"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="methodGroup">The method-group argument expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the referenced source method always returns true.</returns>
    internal static bool IsAlwaysTrueMethodGroup(SemanticModel model, ExpressionSyntax methodGroup, CancellationToken cancellationToken)
    {
        // A method group without exactly one source declaration (metadata, partial, or overload set) cannot be
        // inspected locally, so it is left alone.
        if (model.GetSymbolInfo(methodGroup, cancellationToken).Symbol is not IMethodSymbol method
            || method.DeclaringSyntaxReferences.Length != 1)
        {
            return false;
        }

        return method.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) switch
        {
            MethodDeclarationSyntax declaration => IsAlwaysTrueBody(declaration.ExpressionBody?.Expression, declaration.Body),
            LocalFunctionStatementSyntax localFunction => IsAlwaysTrueBody(localFunction.ExpressionBody?.Expression, localFunction.Body),
            _ => false,
        };
    }

    /// <summary>Returns whether a member body always yields <see langword="true"/>.</summary>
    /// <param name="expressionBody">The arrow-body expression, when the member is expression-bodied.</param>
    /// <param name="block">The block body, when the member is block-bodied.</param>
    /// <returns><see langword="true"/> for an expression body of <c>true</c> or a block whose only result is <c>return true;</c>.</returns>
    private static bool IsAlwaysTrueBody(ExpressionSyntax? expressionBody, BlockSyntax? block)
    {
        if (expressionBody is not null)
        {
            return IsTrueLiteral(expressionBody);
        }

        return block is not null && BlockAlwaysReturnsTrue(block);
    }

    /// <summary>Returns whether every <c>return</c> in a block yields the literal <see langword="true"/>.</summary>
    /// <param name="block">The block body to inspect.</param>
    /// <returns><see langword="true"/> when the block has at least one return and all of them return <c>true</c>.</returns>
    private static bool BlockAlwaysReturnsTrue(BlockSyntax block)
    {
        var sawReturn = false;
        return AllReturnsTrue(block, ref sawReturn) && sawReturn;
    }

    /// <summary>Walks a body's own <c>return</c> statements, skipping nested functions.</summary>
    /// <param name="node">The current node whose children are scanned.</param>
    /// <param name="sawReturn">Set to <see langword="true"/> when any return belonging to this body is seen.</param>
    /// <returns><see langword="false"/> as soon as a return yields anything other than the literal <c>true</c>.</returns>
    private static bool AllReturnsTrue(SyntaxNode node, ref bool sawReturn)
    {
        foreach (var child in node.ChildNodes())
        {
            switch (child)
            {
                case ReturnStatementSyntax returnStatement:
                {
                    sawReturn = true;
                    if (!IsTrueLiteral(returnStatement.Expression))
                    {
                        return false;
                    }

                    break;
                }

                // A nested lambda, anonymous method, or local function owns its own returns; do not descend.
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    break;

                default:
                {
                    if (!AllReturnsTrue(child, ref sawReturn))
                    {
                        return false;
                    }

                    break;
                }
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression is the literal <see langword="true"/>, ignoring parentheses.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> when the expression is a <c>true</c> literal.</returns>
    private static bool IsTrueLiteral(ExpressionSyntax? expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression?.IsKind(SyntaxKind.TrueLiteralExpression) == true;
    }
}
