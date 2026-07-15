// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Syntactic recognizers for LINQ predicate and ordering calls, shared between the Collections
/// analyzers and their code fixes. None of these bind: they read the shape of a call before the
/// semantic model is consulted, so they cost nothing on the clean path.
/// </summary>
internal static class LinqCallSyntax
{
    /// <summary>Gets an invocation's single one-parameter lambda argument.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="lambda">The lambda argument.</param>
    /// <returns><see langword="true"/> when the only argument is a lambda with exactly one parameter.</returns>
    public static bool TryGetOneParameterLambda(InvocationExpressionSyntax invocation, out LambdaExpressionSyntax lambda)
    {
        lambda = null!;
        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        switch (invocation.ArgumentList.Arguments[0].Expression)
        {
            case SimpleLambdaExpressionSyntax simple:
                {
                    lambda = simple;
                    return true;
                }

            case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized:
                {
                    lambda = parenthesized;
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Gets the parameter name and expression body of a one-parameter lambda argument.</summary>
    /// <param name="argument">The argument expression.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <param name="expressionBody">The lambda expression body, or <see langword="null"/> for statement bodies.</param>
    /// <returns><see langword="true"/> when the argument is a one-parameter lambda.</returns>
    public static bool TryGetPredicateLambda(ExpressionSyntax argument, out string parameterName, out ExpressionSyntax? expressionBody)
    {
        switch (argument)
        {
            case SimpleLambdaExpressionSyntax simple:
                {
                    parameterName = simple.Parameter.Identifier.ValueText;
                    expressionBody = simple.ExpressionBody;
                    return true;
                }

            case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized:
                {
                    parameterName = parenthesized.ParameterList.Parameters[0].Identifier.ValueText;
                    expressionBody = parenthesized.ExpressionBody;
                    return true;
                }

            default:
                {
                    parameterName = null!;
                    expressionBody = null;
                    return false;
                }
        }
    }

    /// <summary>Gets the non-parameter side of a <c>param == expr</c> or <c>expr == param</c> equality.</summary>
    /// <param name="equality">The equality expression.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <param name="value">The compared value expression.</param>
    /// <returns><see langword="true"/> when one side is exactly the lambda parameter.</returns>
    public static bool TryGetComparedValue(BinaryExpressionSyntax equality, string parameterName, out ExpressionSyntax value)
    {
        if (equality.Left is IdentifierNameSyntax left && left.Identifier.ValueText == parameterName)
        {
            value = equality.Right;
            return true;
        }

        if (equality.Right is IdentifierNameSyntax right && right.Identifier.ValueText == parameterName)
        {
            value = equality.Left;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>Returns whether the method name is a LINQ sort operator.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for the four LINQ sort operators.</returns>
    public static bool IsSortMethodName(string name)
        => name is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending";
}
