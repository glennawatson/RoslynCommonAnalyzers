// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Reads the shape of a call's written arguments without binding it.</summary>
internal static class ArgumentListFacts
{
    /// <summary>Returns whether any argument is named or passed with <c>ref</c>, <c>out</c> or <c>in</c>.</summary>
    /// <param name="invocation">The call to inspect.</param>
    /// <returns><see langword="true"/> when an argument is not a plain positional value, so the arguments cannot be moved into another call as written.</returns>
    internal static bool HasNonPositionalArgument(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is not null || !arguments[i].RefOrOutKeyword.IsKind(SyntaxKind.None))
            {
                return true;
            }
        }

        return false;
    }
}
