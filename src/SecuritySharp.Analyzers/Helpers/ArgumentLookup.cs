// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Maps a parameter to the argument that supplies it, shared by the rules that inspect one argument of a
/// guarded call or attribute. An argument that names the parameter wins; otherwise the argument at the
/// parameter's position counts, but only when that argument does not name a different parameter.
/// </summary>
internal static class ArgumentLookup
{
    /// <summary>Returns the argument supplying a parameter of a call or object creation.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="parameterName">The parameter's name.</param>
    /// <param name="ordinal">The parameter's zero-based position, or -1 when the overload has no such parameter.</param>
    /// <returns>The supplying argument, or <see langword="null"/> when none can be identified.</returns>
    internal static ArgumentSyntax? Find(SeparatedSyntaxList<ArgumentSyntax> arguments, string parameterName, int ordinal)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { } nameColon && nameColon.Name.Identifier.ValueText == parameterName)
            {
                return arguments[i];
            }
        }

        return ordinal >= 0 && ordinal < arguments.Count && arguments[ordinal].NameColon is null
            ? arguments[ordinal]
            : null;
    }

    /// <summary>Returns the argument supplying a constructor parameter of an attribute.</summary>
    /// <param name="arguments">The attribute's arguments.</param>
    /// <param name="parameterName">The constructor parameter's name.</param>
    /// <param name="ordinal">The parameter's zero-based position, or -1 when the constructor has no such parameter.</param>
    /// <returns>The supplying argument, or <see langword="null"/> when none can be identified.</returns>
    /// <remarks>A <c>Name = value</c> property assignment at the position never supplies a constructor parameter.</remarks>
    internal static AttributeArgumentSyntax? Find(SeparatedSyntaxList<AttributeArgumentSyntax> arguments, string parameterName, int ordinal)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { } nameColon && nameColon.Name.Identifier.ValueText == parameterName)
            {
                return arguments[i];
            }
        }

        return ordinal >= 0 && ordinal < arguments.Count && arguments[ordinal].NameColon is null && arguments[ordinal].NameEquals is null
            ? arguments[ordinal]
            : null;
    }

    /// <summary>Returns the argument supplying a bound method's parameter, located by its name.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="method">The bound method or constructor.</param>
    /// <param name="parameterName">The parameter's name.</param>
    /// <returns>The supplying argument, or <see langword="null"/> when none can be identified or the overload has no such parameter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ArgumentSyntax? FindForParameter(SeparatedSyntaxList<ArgumentSyntax> arguments, IMethodSymbol method, string parameterName) =>
        Find(arguments, parameterName, IndexOfParameter(method, parameterName));

    /// <summary>Returns the position of a named parameter in a bound method.</summary>
    /// <param name="method">The bound method or constructor.</param>
    /// <param name="parameterName">The parameter's name.</param>
    /// <returns>The zero-based position, or -1 when the method has no parameter of that name.</returns>
    internal static int IndexOfParameter(IMethodSymbol method, string parameterName)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == parameterName)
            {
                return i;
            }
        }

        return -1;
    }
}
