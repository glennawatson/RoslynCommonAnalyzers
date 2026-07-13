// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Shared guards for the rules that rewrite an expression into a span, a slice, or a hoisted
/// field. Each one answers a question that has to be settled before a rewrite is offered, and
/// getting any of them wrong produces a fix that does not compile or does not mean the same thing.
/// </summary>
internal static class SpanRewriteGuard
{
    /// <summary>Returns whether a node sits inside an expression tree or a query expression.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a rewrite must not be offered here.</returns>
    /// <remarks>
    /// An expression tree cannot hold a <c>ref struct</c>, so introducing a span inside one stops
    /// compiling even though the same span is fine everywhere else, and an <c>IQueryable</c> source
    /// turns a query's lambdas into trees whose translation depends on the exact call shape. Neither
    /// is a place to change a call behind the author's back.
    /// </remarks>
    public static bool IsInsideExpressionTree(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is QueryExpressionSyntax)
            {
                return true;
            }

            if (current is LambdaExpressionSyntax && IsExpressionTreeType(model.GetTypeInfo(current, cancellationToken).ConvertedType))
            {
                return true;
            }

            if (current is MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether an expression can be evaluated twice, or dropped, without changing what the
    /// program does.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when the expression is a literal or a plain name path.</returns>
    /// <remarks>
    /// A rule that proves <c>s.Substring(i, s.Length - i)</c> reaches the end has to compare the two
    /// spellings of <c>s</c> and of <c>i</c>, and its fix then deletes one of each. That is only sound
    /// when re-reading the expression yields the same value and evaluating it has no side effect, so
    /// the comparison is restricted to literals and dotted name paths — never a call, an indexer, or
    /// anything else that could do work. <c>GetText().Substring(i, GetText().Length - i)</c> is left
    /// alone precisely because dropping the second call is not a no-op.
    /// </remarks>
    public static bool IsRepeatable(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax or ThisExpressionSyntax or BaseExpressionSyntax or PredefinedTypeSyntax or LiteralExpressionSyntax => true,
            MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access => IsRepeatable(access.Expression),
            ParenthesizedExpressionSyntax parenthesized => IsRepeatable(parenthesized.Expression),
            _ => false,
        };

    /// <summary>Returns whether a type is <see cref="StringComparison"/>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for <c>System.StringComparison</c>.</returns>
    public static bool IsStringComparison(ITypeSymbol? type)
        => type is INamedTypeSymbol
        {
            Name: nameof(StringComparison),
            TypeKind: TypeKind.Enum,
            ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
        };

    /// <summary>Returns whether a simple name resolves to a type in the <c>System</c> namespace at a position.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The lookup position.</param>
    /// <param name="name">The simple type name.</param>
    /// <returns><see langword="true"/> when the unqualified spelling binds to the System type.</returns>
    public static bool ResolvesInSystem(SemanticModel model, int position, string name)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: name))
        {
            if (candidate is INamedTypeSymbol { ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a converted type is <c>System.Linq.Expressions.Expression</c>.</summary>
    /// <param name="type">The lambda's converted type.</param>
    /// <returns><see langword="true"/> when the lambda becomes an expression tree.</returns>
    private static bool IsExpressionTreeType(ITypeSymbol? type)
        => type is INamedTypeSymbol
        {
            Name: "Expression",
            ContainingNamespace:
            {
                Name: "Expressions",
                ContainingNamespace: { Name: "Linq", ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } },
            },
        };
}
