// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Renames the member a member-access invocation calls, leaving its receiver and arguments as written.</summary>
internal static class InvokedNameRename
{
    /// <summary>Builds the edit that renames the invoked member.</summary>
    /// <param name="invocation">An invocation whose callee is a member access.</param>
    /// <param name="newName">The member name to call instead.</param>
    /// <returns>The name node and its replacement, which keeps the original name's trivia.</returns>
    internal static NodeReplacement Replace(InvocationExpressionSyntax invocation, string newName)
    {
        var name = ((MemberAccessExpressionSyntax)invocation.Expression).Name;
        return new(
            name,
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(name.GetLeadingTrivia(), newName, name.GetTrailingTrivia())));
    }
}
