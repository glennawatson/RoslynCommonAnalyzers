// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads the configured instance-member qualification style.</summary>
internal static class InstanceMemberQualificationOptions
{
    /// <summary>The project-wide option that controls instance-member qualification.</summary>
    public const string GeneralKey = "stylesharp.instance_member_qualification";

    /// <summary>The default StyleSharp style: fields and members are read without <c>this.</c>.</summary>
    public const InstanceMemberQualification Default = InstanceMemberQualification.OmitThis;

    /// <summary>Reads the configured instance-member qualification style.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <returns>The configured style, or the default when the option is unset or unknown.</returns>
    public static InstanceMemberQualification Read(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(GeneralKey, out var value))
        {
            return Default;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "require_this", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "this", StringComparison.OrdinalIgnoreCase))
        {
            return InstanceMemberQualification.RequireThis;
        }

        return string.Equals(trimmed, "omit_this", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "omit", StringComparison.OrdinalIgnoreCase)
            ? InstanceMemberQualification.OmitThis
            : Default;
    }
}
