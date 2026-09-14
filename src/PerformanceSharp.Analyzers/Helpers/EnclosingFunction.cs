// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Finds the body of the innermost function a node runs in, shared by the rules that bound a name scan to
/// the function that owns a local (PSH1316, PSH1408).
/// </summary>
internal static class EnclosingFunction
{
    /// <summary>Returns the body of the innermost lambda, anonymous method, local function, method, constructor, operator, or accessor enclosing a node.</summary>
    /// <param name="node">The node whose enclosing function body is sought.</param>
    /// <returns>The block or expression body, or <see langword="null"/> when no function encloses the node.</returns>
    /// <remarks>A type declaration or the compilation unit ends the walk: nothing above either is a function body.</remarks>
    internal static SyntaxNode? GetBody(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax anonymousFunction:
                    return anonymousFunction.Body;
                case LocalFunctionStatementSyntax localFunction:
                    return (SyntaxNode?)localFunction.Body ?? localFunction.ExpressionBody;
                case BaseMethodDeclarationSyntax method:
                    return (SyntaxNode?)method.Body ?? method.ExpressionBody;
                case AccessorDeclarationSyntax accessor:
                    return (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody;
                case BaseTypeDeclarationSyntax or CompilationUnitSyntax:
                    return null;
                default:
                    continue;
            }
        }

        return null;
    }
}
