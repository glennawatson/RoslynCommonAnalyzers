// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Shared suffix and configuration logic for SST1704 extension-container naming.</summary>
internal static class ExtensionContainerNaming
{
    /// <summary>The default and historically preferred suffix.</summary>
    public const string ExtensionsSuffix = "Extensions";

    /// <summary>The alternate accepted suffix.</summary>
    public const string MixinsSuffix = "Mixins";

    /// <summary>The SST1704-specific preferred-suffix option key.</summary>
    public const string PreferredSuffixSpecificKey = "stylesharp.SST1704.preferred_suffix";

    /// <summary>The general preferred-suffix option key.</summary>
    public const string PreferredSuffixGeneralKey = "stylesharp.extension_container_preferred_suffix";

    /// <summary>Returns whether the container name already ends with an accepted suffix.</summary>
    /// <param name="name">The container class name.</param>
    /// <returns><see langword="true"/> when the name ends with <c>Extensions</c> or <c>Mixins</c>.</returns>
    public static bool HasValidSuffix(string name)
        => name.EndsWith(ExtensionsSuffix, StringComparison.Ordinal)
            || name.EndsWith(MixinsSuffix, StringComparison.Ordinal);

    /// <summary>Reads the preferred extension-container suffix from analyzer config options.</summary>
    /// <param name="options">The options to inspect.</param>
    /// <returns><c>Extensions</c> or <c>Mixins</c>, defaulting to <c>Extensions</c>.</returns>
    public static string ReadPreferredSuffix(AnalyzerConfigOptions options)
    {
        if (TryReadPreferredSuffix(options, PreferredSuffixSpecificKey, out var preferredSuffix)
            || TryReadPreferredSuffix(options, PreferredSuffixGeneralKey, out preferredSuffix))
        {
            return preferredSuffix!;
        }

        return ExtensionsSuffix;
    }

    /// <summary>Builds the preferred container name by removing any accepted suffix before appending the preferred one.</summary>
    /// <param name="currentName">The current container name.</param>
    /// <param name="preferredSuffix">The preferred suffix to append.</param>
    /// <returns>The preferred container name.</returns>
    public static string BuildPreferredName(string currentName, string preferredSuffix)
    {
        var baseName = currentName;
        if (baseName.EndsWith(ExtensionsSuffix, StringComparison.Ordinal))
        {
            baseName = baseName[..^ExtensionsSuffix.Length];
        }
        else if (baseName.EndsWith(MixinsSuffix, StringComparison.Ordinal))
        {
            baseName = baseName[..^MixinsSuffix.Length];
        }

        return baseName + preferredSuffix;
    }

    /// <summary>Tries to read and validate one preferred-suffix option key.</summary>
    /// <param name="options">The options to inspect.</param>
    /// <param name="key">The option key.</param>
    /// <param name="preferredSuffix">The validated preferred suffix.</param>
    /// <returns><see langword="true"/> when a recognized value is present.</returns>
    private static bool TryReadPreferredSuffix(AnalyzerConfigOptions options, string key, out string? preferredSuffix)
    {
        preferredSuffix = null;
        if (!options.TryGetValue(key, out var value) || value.Length == 0)
        {
            return false;
        }

        if (string.Equals(value, ExtensionsSuffix, StringComparison.OrdinalIgnoreCase))
        {
            preferredSuffix = ExtensionsSuffix;
            return true;
        }

        preferredSuffix = string.Equals(value, MixinsSuffix, StringComparison.OrdinalIgnoreCase)
            ? MixinsSuffix
            : null;
        return preferredSuffix is not null;
    }
}
