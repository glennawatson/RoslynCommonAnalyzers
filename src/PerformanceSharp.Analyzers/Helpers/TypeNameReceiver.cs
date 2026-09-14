// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Free syntax gates for a static call written against a framework type's name — <c>Thread.Sleep</c>,
/// <c>Task.Delay</c>, <c>Debug.Assert</c> and the like — shared by the rules that bind only calls passing
/// the gate. Matching the written name over-approximates: a real call always passes, and anything else
/// spelled the same is turned back by the binding that follows.
/// </summary>
internal static class TypeNameReceiver
{
    /// <summary>Returns whether a receiver's rightmost identifier is a type name, looking through any namespace qualification.</summary>
    /// <param name="receiver">The receiver of the invoked member access.</param>
    /// <param name="typeName">The simple type name to match.</param>
    /// <returns><see langword="true"/> for <c>Name</c> and <c>Namespace.Name</c>.</returns>
    internal static bool EndsWithTypeName(ExpressionSyntax receiver, string typeName)
    {
        var current = receiver;
        while (current is MemberAccessExpressionSyntax nested)
        {
            current = nested.Name;
        }

        return current is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == typeName;
    }

    /// <summary>Returns whether an invocation calls a named member on a receiver written as a type name.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="methodName">The invoked member name.</param>
    /// <param name="typeName">The simple type name the receiver must end in.</param>
    /// <returns><see langword="true"/> for <c>TypeName.MethodName(...)</c>, qualified or not.</returns>
    internal static bool IsCallOnTypeName(InvocationExpressionSyntax invocation, string methodName, string typeName) =>
        invocation.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText == methodName
            && EndsWithTypeName(access.Expression, typeName);
}
