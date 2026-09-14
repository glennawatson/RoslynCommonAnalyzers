// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Matches written names against the parameters and type parameters a declaration introduces, without binding.</summary>
internal static class ParameterNames
{
    /// <summary>Returns whether a parameter list declares a parameter with the given name.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when a parameter has the name.</returns>
    internal static bool Contains(SeparatedSyntaxList<ParameterSyntax> parameters, string name)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type-parameter list declares a type parameter with the given name.</summary>
    /// <param name="typeParameters">The type parameters.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when a type parameter has the name.</returns>
    internal static bool Contains(SeparatedSyntaxList<TypeParameterSyntax> typeParameters, string name)
    {
        for (var i = 0; i < typeParameters.Count; i++)
        {
            if (typeParameters[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }
}
