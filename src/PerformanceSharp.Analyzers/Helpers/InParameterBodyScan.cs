// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Decides whether a member's body would still compile if one of its parameters became <c>in</c>.
/// </summary>
/// <remarks>
/// An <c>in</c> parameter is a readonly reference, and the compiler rejects four things a by-value
/// parameter allows. Each maps to a real error, so missing one of them turns the diagnostic into advice
/// that does not build:
/// <list type="bullet">
/// <item><description>A <c>yield</c> makes the member an iterator, which cannot take <c>in</c> (CS1623).</description></item>
/// <item><description>Reading the parameter inside a lambda, local function, or query captures it (CS1628).</description></item>
/// <item><description>Assigning the parameter writes through a readonly reference (CS8331).</description></item>
/// <item><description>Passing the parameter as a <c>ref</c> or <c>out</c> argument does the same (CS8329).</description></item>
/// </list>
/// The walk is one indexed preorder pass over the body, and the semantic model is only consulted for an
/// identifier whose text already matches the parameter's name.
/// </remarks>
internal static class InParameterBodyScan
{
    /// <summary>Returns whether the body permits the parameter to become an <c>in</c> parameter.</summary>
    /// <param name="body">The member's body, or <see langword="null"/> when it has none.</param>
    /// <param name="parameter">The parameter under consideration.</param>
    /// <param name="model">The semantic model for the body's tree.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when nothing in the body would stop the change from compiling.</returns>
    public static bool CanBecomeReadonlyReference(
        SyntaxNode? body,
        IParameterSymbol parameter,
        SemanticModel model,
        CancellationToken cancellationToken)
        => body is null || Visit(body, parameter, model, insideNestedFunction: false, cancellationToken);

    /// <summary>Walks one node and its descendants, tracking whether the walk has entered a nested function.</summary>
    /// <param name="node">The current node.</param>
    /// <param name="parameter">The parameter under consideration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="insideNestedFunction">Whether the walk is inside a lambda, local function, or query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the subtree permits the change.</returns>
    private static bool Visit(
        SyntaxNode node,
        IParameterSymbol parameter,
        SemanticModel model,
        bool insideNestedFunction,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A yield inside a nested function makes that function an iterator, not this member.
        if (node is YieldStatementSyntax && !insideNestedFunction)
        {
            return false;
        }

        if (IsNestedFunction(node))
        {
            insideNestedFunction = true;
        }

        if (node is IdentifierNameSyntax identifier
            && Disqualifies(identifier, parameter, model, insideNestedFunction, cancellationToken))
        {
            return false;
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is { } child
                && !Visit(child, parameter, model, insideNestedFunction, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a node opens a scope that would capture the parameter.</summary>
    /// <param name="node">The current node.</param>
    /// <returns><see langword="true"/> for a lambda, anonymous method, local function, or query.</returns>
    private static bool IsNestedFunction(SyntaxNode node)
        => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or QueryExpressionSyntax;

    /// <summary>Returns whether one identifier rules the parameter out of becoming an <c>in</c>.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="parameter">The parameter under consideration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="insideNestedFunction">Whether the identifier sits inside a nested function.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the parameter is captured or written.</returns>
    private static bool Disqualifies(
        IdentifierNameSyntax identifier,
        IParameterSymbol parameter,
        SemanticModel model,
        bool insideNestedFunction,
        CancellationToken cancellationToken)
        => identifier.Identifier.ValueText == parameter.Name
            && IsParameterReference(identifier, parameter, model, cancellationToken)
            && (insideNestedFunction || IsWritten(identifier));

    /// <summary>Returns whether an identifier binds to the parameter rather than shadowing its name.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="parameter">The parameter under consideration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the identifier is the parameter.</returns>
    private static bool IsParameterReference(
        IdentifierNameSyntax identifier,
        IParameterSymbol parameter,
        SemanticModel model,
        CancellationToken cancellationToken)
        => SymbolEqualityComparer.Default.Equals(
            model.GetSymbolInfo(identifier, cancellationToken).Symbol,
            parameter);

    /// <summary>Returns whether an identifier is written to, rather than only read.</summary>
    /// <param name="identifier">The identifier that binds to the parameter.</param>
    /// <returns><see langword="true"/> when the parameter is assigned or passed by reference.</returns>
    private static bool IsWritten(IdentifierNameSyntax identifier) => identifier.Parent switch
    {
        // Simple and compound assignment alike write to the left operand.
        AssignmentExpressionSyntax assignment => ReferenceEquals(assignment.Left, identifier),

        // A postfix operator can only be an increment or a decrement, so it always writes.
        PostfixUnaryExpressionSyntax => true,
        PrefixUnaryExpressionSyntax prefix => prefix.Kind()
            is SyntaxKind.PreIncrementExpression
            or SyntaxKind.PreDecrementExpression,

        // An argument passed by reference may be written by the callee.
        ArgumentSyntax argument => argument.RefKindKeyword.Kind()
            is SyntaxKind.RefKeyword
            or SyntaxKind.OutKeyword,

        _ => false,
    };
}
