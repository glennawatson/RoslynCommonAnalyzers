// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Maps an attribute's written arguments to the constructor parameters they supply, in one pass over the list.</summary>
internal static class AttributeArgumentParameter
{
    /// <summary>Returns the constructor parameter an attribute argument supplies, advancing the positional cursor.</summary>
    /// <param name="argument">The attribute argument, visited in written order.</param>
    /// <param name="constructor">The bound attribute constructor.</param>
    /// <param name="positional">The number of positional arguments already visited; incremented for a positional argument.</param>
    /// <returns>
    /// The parameter name, or <see langword="null"/> for a <c>Name = value</c> property assignment or a positional
    /// argument beyond the constructor's parameters.
    /// </returns>
    internal static string? NameOf(AttributeArgumentSyntax argument, IMethodSymbol constructor, ref int positional)
    {
        if (argument.NameEquals is not null)
        {
            return null;
        }

        if (argument.NameColon is { } nameColon)
        {
            return nameColon.Name.Identifier.ValueText;
        }

        var parameters = constructor.Parameters;
        var name = positional < parameters.Length ? parameters[positional].Name : null;
        positional++;
        return name;
    }
}
