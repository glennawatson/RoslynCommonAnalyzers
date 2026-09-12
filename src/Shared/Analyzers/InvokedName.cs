// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Reads the simple method name an invocation targets, ignoring the receiver. A rule that only needs to
/// know which method is being called can filter on the name before asking for a symbol, which keeps the
/// no-diagnostic path off the semantic model entirely.
/// </summary>
internal static class InvokedName
{
    /// <summary>Returns the simple method name an invocation targets, ignoring the receiver.</summary>
    /// <param name="invoked">The invocation's callee expression.</param>
    /// <returns>The simple method name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    internal static string? Of(ExpressionSyntax invoked) =>
        invoked switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };
}
