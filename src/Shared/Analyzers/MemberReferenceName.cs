// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Reads the name a plain identifier or a <c>receiver.Name</c> member access refers to, ignoring the receiver.
/// A rule can filter an assignment target, a callee, or an operand on that name before binding anything.
/// </summary>
/// <remarks>
/// Unlike <see cref="InvokedName"/>, a conditional member binding (<c>receiver?.Name</c>) has no name here, so a
/// rule that only reasons about unconditional access never matches the null-conditional form.
/// </remarks>
internal static class MemberReferenceName
{
    /// <summary>Returns the simple name an identifier or a member access refers to.</summary>
    /// <param name="expression">The expression to read.</param>
    /// <returns>The simple name, or <see langword="null"/> for any other expression.</returns>
    internal static string? Of(ExpressionSyntax expression) =>
        expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };
}
