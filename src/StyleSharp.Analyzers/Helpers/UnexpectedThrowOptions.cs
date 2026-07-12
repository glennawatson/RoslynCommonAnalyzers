// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1485 settings for one syntax tree.</summary>
/// <param name="AdditionalMembers">The extra member names that must not throw, exactly as configured.</param>
/// <remarks>
/// The list is kept as the raw configured text and scanned in place. It is empty in almost every tree, and
/// an empty scan neither allocates nor compares anything.
/// </remarks>
internal readonly record struct UnexpectedThrowOptions(string AdditionalMembers)
{
    /// <summary>The rule-specific additional-members key.</summary>
    private const string AdditionalMembersRuleKey = "stylesharp.SST1485.additional_members";

    /// <summary>The project-wide additional-members key.</summary>
    private const string AdditionalMembersGeneralKey = "stylesharp.additional_members";

    /// <summary>Gets the settings used when nothing is configured.</summary>
    public static UnexpectedThrowOptions Default => new(string.Empty);

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the member's tree.</param>
    /// <returns>The resolved settings.</returns>
    public static UnexpectedThrowOptions Read(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(AdditionalMembersRuleKey, out var value)
            && !options.TryGetValue(AdditionalMembersGeneralKey, out value))
        {
            return Default;
        }

        return new UnexpectedThrowOptions(value);
    }

    /// <summary>Returns whether a member name was added to the must-not-throw list.</summary>
    /// <param name="name">The member's name.</param>
    /// <returns><see langword="true"/> when the configuration names the member.</returns>
    public bool Contains(string name)
        => AdditionalMembers.Length != 0 && EditorConfigList.Contains(AdditionalMembers, name, StringComparison.Ordinal);
}
