// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Binds a declaration's attributes in source order and matches each by attribute class — a subclass of a
/// marker counts — shared by the Blazor component rules (SES1703, SES1704).
/// </summary>
internal static class ComponentAttributeScan
{
    /// <summary>Returns the first attribute deriving from a stop marker, recording the first attribute deriving from a second marker seen before the scan stops.</summary>
    /// <param name="model">The semantic model for the declaration's tree.</param>
    /// <param name="attributeLists">The declaration's attribute lists.</param>
    /// <param name="stopMarker">The marker whose first match ends the scan.</param>
    /// <param name="recordMarker">The marker to record along the way, or <see langword="null"/> to record nothing.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="recorded">The first attribute deriving from <paramref name="recordMarker"/> before the scan stopped, or <see langword="null"/>.</param>
    /// <returns>The first attribute deriving from <paramref name="stopMarker"/>, or <see langword="null"/> when none does.</returns>
    /// <remarks>An attribute is tested against the stop marker first, so one deriving from both markers stops the scan.</remarks>
    internal static AttributeSyntax? FindFirst(
        SemanticModel model,
        SyntaxList<AttributeListSyntax> attributeLists,
        INamedTypeSymbol stopMarker,
        INamedTypeSymbol? recordMarker,
        CancellationToken cancellationToken,
        out AttributeSyntax? recorded)
    {
        recorded = null;
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attributeType = BlazorComponentHelper.GetAttributeType(model, attributes[j], cancellationToken);
                if (TypeRelations.IsOrDerivesFrom(attributeType, stopMarker))
                {
                    return attributes[j];
                }

                if (recorded is null && recordMarker is not null && TypeRelations.IsOrDerivesFrom(attributeType, recordMarker))
                {
                    recorded = attributes[j];
                }
            }
        }

        return null;
    }
}
