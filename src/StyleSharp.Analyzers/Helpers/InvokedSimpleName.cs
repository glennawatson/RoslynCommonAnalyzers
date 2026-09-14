// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the simple name an invocation calls, ignoring the receiver and any type arguments written on the name. A bare
/// generic call such as <c>Equal&lt;int&gt;(a, b)</c> answers with its name, as a member access or member binding does.
/// </summary>
internal static class InvokedSimpleName
{
    /// <summary>Returns the name an invocation calls.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked name, or <see langword="null"/> when the callee is not a simple name, member access, or member binding.</returns>
    internal static string? Of(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns the location of the name an invocation calls, for a diagnostic.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns>The name's location, or the whole invocation's when it has no simple name.</returns>
    internal static Location LocationOf(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.GetLocation(),
        MemberBindingExpressionSyntax binding => binding.Name.GetLocation(),
        SimpleNameSyntax simple => simple.GetLocation(),
        _ => invocation.GetLocation(),
    };
}
