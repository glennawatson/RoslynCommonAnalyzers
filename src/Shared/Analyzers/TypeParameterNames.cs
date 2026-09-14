// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace RoslynCommon.Analyzers;

/// <summary>Writes a declaration's type-parameter names for display names and file names.</summary>
internal static class TypeParameterNames
{
    /// <summary>Appends the type-parameter names joined by a separator.</summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="typeParameters">The declared type-parameter list.</param>
    /// <param name="separator">The text between two names.</param>
    /// <returns>The same builder, for chaining the closing text.</returns>
    internal static StringBuilder AppendJoined(StringBuilder builder, TypeParameterListSyntax typeParameters, string separator)
    {
        var parameters = typeParameters.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                _ = builder.Append(separator);
            }

            _ = builder.Append(parameters[i].Identifier.ValueText);
        }

        return builder;
    }
}
