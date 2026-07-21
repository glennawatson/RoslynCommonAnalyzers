// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// The single shared helper for the Blazor input rules (SES1705, SES1706): it reads the simple name of
/// an invocation's callee for the cheap syntactic prefilter, and locates the argument bound to a target
/// parameter honouring an explicit <c>name:</c> label before falling back to a positional slot. Both
/// Blazor analyzers filter on a member name and then pull one specific argument, so that logic is kept
/// here in one copy rather than duplicated in each analyzer.
/// </summary>
internal static class BlazorInvocation
{
    /// <summary>Returns the invoked member's simple name for an <c>Identifier(...)</c> or <c>x.Identifier(...)</c> call.</summary>
    /// <param name="expression">The invocation's callee expression.</param>
    /// <returns>The simple name, or <see langword="null"/> when the callee is not a plain member reference.</returns>
    public static string? GetInvokedName(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the argument bound to a parameter, honouring an explicit <c>name:</c> label then a positional slot.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <param name="parameterName">The target parameter's name, matched against any <c>name:</c> label.</param>
    /// <param name="position">The zero-based positional slot the parameter occupies.</param>
    /// <returns>The argument expression, or <see langword="null"/> when it cannot be identified positionally.</returns>
    public static ExpressionSyntax? GetArgument(ArgumentListSyntax argumentList, string parameterName, int position)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { } nameColon && string.Equals(nameColon.Name.Identifier.ValueText, parameterName, StringComparison.Ordinal))
            {
                return arguments[i].Expression;
            }
        }

        // A positional match holds only when nothing at or before the slot is named, since a name shifts the mapping.
        if (arguments.Count <= position)
        {
            return null;
        }

        for (var i = 0; i <= position; i++)
        {
            if (arguments[i].NameColon is not null)
            {
                return null;
            }
        }

        return arguments[position].Expression;
    }
}
