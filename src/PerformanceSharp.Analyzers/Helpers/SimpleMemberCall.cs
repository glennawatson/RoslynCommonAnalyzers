// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Syntax gates for a call made through a plain <c>receiver.Name(...)</c> member access, shared by the string
/// rules that recognize a call by name before binding it (PSH1211, PSH1221, PSH1224, PSH1226). A conditional
/// access (<c>receiver?.Name()</c>) and a pointer member access never match.
/// </summary>
internal static class SimpleMemberCall
{
    /// <summary>Returns whether an invocation calls a named member through a simple member access.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="memberName">The member name.</param>
    /// <returns><see langword="true"/> for <c>receiver.MemberName(...)</c>.</returns>
    internal static bool IsNamed(InvocationExpressionSyntax invocation, string memberName) =>
        invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == memberName;

    /// <summary>Returns whether an expression is a named simple member call that passes at least one argument.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="memberName">The member name.</param>
    /// <returns><see langword="true"/> for <c>receiver.MemberName(argument, ...)</c>.</returns>
    internal static bool IsNamedWithArguments(ExpressionSyntax expression, string memberName) =>
        expression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } invocation
            && IsNamed(invocation, memberName);
}
