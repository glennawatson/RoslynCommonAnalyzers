// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST2313 settings for one syntax tree: which underlying types an enum may use.</summary>
/// <param name="AllowedStorage">The configured list of allowed storage types, as written in the config.</param>
/// <remarks>
/// The list is kept as the raw config text rather than a parsed set: it is matched against at most one
/// type name per enum that declares an underlying type, which is rare enough that scanning the string
/// beats allocating a set for every tree.
/// </remarks>
internal readonly record struct EnumStorageOptions(string AllowedStorage)
{
    /// <summary>The default allowed storage: <c>int</c>, the underlying type an enum gets when it names none.</summary>
    public const string DefaultAllowedStorage = "int";

    /// <summary>The rule-specific allowed-storage key.</summary>
    private const string AllowedStorageRuleKey = "stylesharp.SST2313.allowed_enum_storage";

    /// <summary>The project-wide allowed-storage key.</summary>
    private const string AllowedStorageGeneralKey = "stylesharp.allowed_enum_storage";

    /// <summary>Reads the settings for one tree, falling back to the default.</summary>
    /// <param name="options">The analyzer config options for the enum's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// An unset or empty value yields the default rather than an empty list, so a typo does not turn every
    /// enum in the project into a diagnostic.
    /// </remarks>
    public static EnumStorageOptions Read(AnalyzerConfigOptions options)
    {
        if (TryReadList(options, AllowedStorageRuleKey, out var list) || TryReadList(options, AllowedStorageGeneralKey, out list))
        {
            return new EnumStorageOptions(list);
        }

        return new EnumStorageOptions(DefaultAllowedStorage);
    }

    /// <summary>Returns whether an enum may be stored as the supplied type.</summary>
    /// <param name="keyword">The C# keyword for the underlying type, such as <c>byte</c>.</param>
    /// <param name="metadataName">The CLR name for the underlying type, such as <c>Byte</c>.</param>
    /// <returns><see langword="true"/> when the configured list names the type either way.</returns>
    /// <remarks>
    /// Both spellings are accepted because both read naturally in a config file, and a team that writes
    /// <c>Int64</c> where the rule expected <c>long</c> should not silently get the default back.
    /// </remarks>
    public bool Allows(string keyword, string metadataName)
        => EditorConfigList.Contains(AllowedStorage, keyword, StringComparison.OrdinalIgnoreCase)
            || EditorConfigList.Contains(AllowedStorage, metadataName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a list setting, treating an empty value as unset.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="key">The key to read.</param>
    /// <param name="list">The configured list.</param>
    /// <returns><see langword="true"/> when the key is present and names at least one type.</returns>
    private static bool TryReadList(AnalyzerConfigOptions options, string key, out string list)
    {
        if (options.TryGetValue(key, out var value) && value.Length != 0)
        {
            list = value;
            return true;
        }

        list = DefaultAllowedStorage;
        return false;
    }
}
