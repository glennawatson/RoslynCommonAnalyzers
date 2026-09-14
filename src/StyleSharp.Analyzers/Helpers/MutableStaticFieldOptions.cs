// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1499 settings for one syntax tree.</summary>
/// <param name="IncludeInternal">Whether a field visible only inside the assembly is reported.</param>
internal readonly record struct MutableStaticFieldOptions(bool IncludeInternal)
{
    /// <summary>Whether an assembly-visible field is reported by default.</summary>
    public const bool DefaultIncludeInternal = true;

    /// <summary>The rule-specific internal-visibility key.</summary>
    private const string IncludeInternalRuleKey = "stylesharp.SST1499.include_internal";

    /// <summary>The project-wide internal-visibility key.</summary>
    private const string IncludeInternalGeneralKey = "stylesharp.include_internal";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the field's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// Assembly-visible fields are included by default: <c>internal</c> is a boundary between teams' code,
    /// not a boundary between threads, and a static field the whole assembly can reassign is exactly as
    /// shared as a public one. An unset or unparsable value keeps that default.
    /// </remarks>
    internal static MutableStaticFieldOptions Read(AnalyzerConfigOptions options) =>
        new(AnalyzerOptionReader.ReadBool(options, IncludeInternalRuleKey, IncludeInternalGeneralKey, DefaultIncludeInternal));
}
