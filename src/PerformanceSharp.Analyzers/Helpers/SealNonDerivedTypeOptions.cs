// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The resolved PSH1411 settings for one syntax tree.</summary>
/// <param name="IncludePublic">Whether externally visible classes are reported.</param>
internal readonly record struct SealNonDerivedTypeOptions(bool IncludePublic)
{
    /// <summary>The rule-specific public-API key.</summary>
    private const string IncludePublicRuleKey = "performancesharp.PSH1411.include_public";

    /// <summary>The project-wide public-API key.</summary>
    private const string IncludePublicGeneralKey = "performancesharp.include_public";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the class's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// The default is <see langword="false"/> because sealing a type another assembly can derive from
    /// is a breaking change, and the compilation cannot see those derivations. Turning it on says
    /// "nothing outside this build derives from my types" — true for an application, rarely true for
    /// a library.
    /// </remarks>
    public static SealNonDerivedTypeOptions Read(AnalyzerConfigOptions options)
        => new(AnalyzerOptionReader.ReadBool(options, IncludePublicRuleKey, IncludePublicGeneralKey));
}
