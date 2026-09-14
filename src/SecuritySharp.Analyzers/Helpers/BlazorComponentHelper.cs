// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;

namespace SecuritySharp.Analyzers;

/// <summary>
/// The attribute-to-class binding the Blazor rules (SES1703, SES1704) share. It binds through the passed-in
/// model, so a marker is matched by attribute class -- including a subclass -- rather than by the written
/// attribute name.
/// </summary>
internal static class BlazorComponentHelper
{
    /// <summary>Binds an attribute to its attribute class (the containing type of its resolved constructor).</summary>
    /// <param name="model">The semantic model for the attribute's tree.</param>
    /// <param name="attribute">The attribute syntax to bind.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bound attribute class, or <see langword="null"/> when the attribute does not resolve.</returns>
    internal static INamedTypeSymbol? GetAttributeType(SemanticModel model, AttributeSyntax attribute, CancellationToken cancellationToken) =>
        model.GetSymbolInfo(attribute, cancellationToken).Symbol is IMethodSymbol { ContainingType: { } attributeType } ? attributeType : null;
}
