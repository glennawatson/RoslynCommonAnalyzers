// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads where a switch statement's <c>default</c> label sits.</summary>
internal static class SwitchLabels
{
    /// <summary>Returns whether a section's label list contains the default label.</summary>
    /// <param name="labels">The section's labels.</param>
    /// <returns><see langword="true"/> when a default label is present.</returns>
    internal static bool ContainsDefault(SyntaxList<SwitchLabelSyntax> labels)
    {
        for (var i = 0; i < labels.Count; i++)
        {
            if (labels[i].IsKind(SyntaxKind.DefaultSwitchLabel))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any section of a switch statement carries the default label.</summary>
    /// <param name="sections">The switch statement's sections.</param>
    /// <returns><see langword="true"/> when a default label is present.</returns>
    internal static bool AnySectionHasDefault(SyntaxList<SwitchSectionSyntax> sections)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (ContainsDefault(sections[i].Labels))
            {
                return true;
            }
        }

        return false;
    }
}
