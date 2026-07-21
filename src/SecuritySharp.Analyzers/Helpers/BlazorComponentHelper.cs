// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;

namespace SecuritySharp.Analyzers;

/// <summary>
/// The minimal symbol helpers the Blazor rules (SES1703, SES1704) share: a base-chain walk and an
/// attribute-to-class binding. Both operate on symbols bound from the passed-in model, so a marker is
/// matched by attribute class -- including a subclass -- rather than by the written attribute name.
/// </summary>
internal static class BlazorComponentHelper
{
    /// <summary>Returns whether a type is, or derives from, a target type by walking its base chain.</summary>
    /// <param name="type">The candidate type (an attribute class or a component type).</param>
    /// <param name="target">The base or marker type to match.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is the target or a subclass of it.</returns>
    public static bool IsOrDerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol target)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Binds an attribute to its attribute class (the containing type of its resolved constructor).</summary>
    /// <param name="model">The semantic model for the attribute's tree.</param>
    /// <param name="attribute">The attribute syntax to bind.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bound attribute class, or <see langword="null"/> when the attribute does not resolve.</returns>
    public static INamedTypeSymbol? GetAttributeType(SemanticModel model, AttributeSyntax attribute, CancellationToken cancellationToken)
        => model.GetSymbolInfo(attribute, cancellationToken).Symbol is IMethodSymbol { ContainingType: { } attributeType } ? attributeType : null;
}
