// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Decides whether a code fix can write a type by its simple name at a position.</summary>
internal static class TypeNameLookup
{
    /// <summary>Returns whether a simple type name binds to a type in a namespace at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <param name="name">The simple type name.</param>
    /// <param name="containingNamespace">The fully qualified namespace the name must resolve into.</param>
    /// <returns><see langword="true"/> when the unqualified spelling binds to a type in that namespace.</returns>
    internal static bool ResolvesIn(SemanticModel model, int position, string name, string containingNamespace)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: name))
        {
            if (candidate is INamedTypeSymbol named && named.ContainingNamespace.ToDisplayString() == containingNamespace)
            {
                return true;
            }
        }

        return false;
    }
}
