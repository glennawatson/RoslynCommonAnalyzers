// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared plumbing for rules that walk a call's arguments against the method's parameters: pulling the
/// argument list off a call, testing whether the method has any optional parameter, and mapping one
/// argument (positional or named) to the parameter it supplies.
/// </summary>
internal static class ArgumentBinding
{
    /// <summary>Returns the argument list of an invocation, an object creation, or a constructor initializer.</summary>
    /// <param name="node">The analyzed node.</param>
    /// <returns>The argument list, or <see langword="null"/> when the node supplies none.</returns>
    public static ArgumentListSyntax? GetArgumentList(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax invocation => invocation.ArgumentList,
        BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
        ConstructorInitializerSyntax initializer => initializer.ArgumentList,
        _ => null,
    };

    /// <summary>Returns whether the called method declares any optional parameter.</summary>
    /// <param name="method">The bound method.</param>
    /// <returns><see langword="true"/> when an optional parameter exists.</returns>
    public static bool HasOptionalParameter(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].IsOptional)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Maps one argument to the parameter it supplies.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="arguments">The argument list.</param>
    /// <param name="index">The argument index.</param>
    /// <returns>The matched parameter, or <see langword="null"/> when the call does not bind cleanly.</returns>
    /// <remarks>
    /// A named argument is matched by name, so it binds even when it appears out of position; a positional
    /// argument binds to the parameter at the same index when one exists.
    /// </remarks>
    public static IParameterSymbol? FindParameter(IMethodSymbol method, SeparatedSyntaxList<ArgumentSyntax> arguments, int index)
    {
        var parameters = method.Parameters;
        if (arguments[index].NameColon is { Name.Identifier.ValueText: var argumentName })
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name == argumentName)
                {
                    return parameters[i];
                }
            }

            return null;
        }

        return index < parameters.Length ? parameters[index] : null;
    }
}
