// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the configurable naming conventions from <c>.editorconfig</c>, following
/// the CA-analyzer approach: the values come from the compiler's
/// <c>AnalyzerConfigOptionsProvider</c> (no file is read directly), and each
/// option is layered rule-specific over general, mirroring CA's
/// <c>dotnet_code_quality.&lt;RuleId&gt;.&lt;option&gt;</c> /
/// <c>dotnet_code_quality.&lt;option&gt;</c> convention under a <c>stylesharp.</c>
/// prefix. Accepted values are <c>pascal_case</c> and <c>camel_case</c>. Keys are
/// constants so a read allocates nothing.
/// </summary>
internal static class NamingConventions
{
    /// <summary>Rule-specific editorconfig key for tuple element casing (SST1316).</summary>
    public const string TupleElementSpecificKey = "stylesharp.SST1316.tuple_element_naming";

    /// <summary>General editorconfig key for tuple element casing.</summary>
    public const string TupleElementGeneralKey = "stylesharp.tuple_element_naming";

    /// <summary>Rule-specific editorconfig key for union member casing (SST1315).</summary>
    public const string UnionMemberSpecificKey = "stylesharp.SST1315.union_member_naming";

    /// <summary>General editorconfig key for union member casing.</summary>
    public const string UnionMemberGeneralKey = "stylesharp.union_member_naming";

    /// <summary>Reads a casing convention, preferring the rule-specific key over the general key.</summary>
    /// <param name="options">The analyzer config options for the relevant syntax tree.</param>
    /// <param name="specificKey">The rule-specific editorconfig key.</param>
    /// <param name="generalKey">The general editorconfig key.</param>
    /// <param name="fallback">The value to use when neither key is set to a recognized value.</param>
    /// <returns>The configured (or fallback) convention.</returns>
    public static NamingConvention Read(AnalyzerConfigOptions options, string specificKey, string generalKey, NamingConvention fallback)
    {
        if (TryRead(options, specificKey, out var convention))
        {
            return convention;
        }

        if (TryRead(options, generalKey, out convention))
        {
            return convention;
        }

        return fallback;
    }

    /// <summary>Returns whether <paramref name="name"/> already conforms to <paramref name="convention"/>.</summary>
    /// <param name="name">The identifier text.</param>
    /// <param name="convention">The expected convention.</param>
    /// <returns><see langword="true"/> when the name conforms.</returns>
    public static bool Conforms(string name, NamingConvention convention)
        => convention == NamingConvention.PascalCase
            ? NamingHelper.BeginsWithUpperCase(name)
            : NamingHelper.BeginsWithLowerCase(name);

    /// <summary>Suggests a corrected form of <paramref name="name"/> for <paramref name="convention"/>.</summary>
    /// <param name="name">The identifier text.</param>
    /// <param name="convention">The expected convention.</param>
    /// <returns>The suggested name.</returns>
    public static string Suggest(string name, NamingConvention convention)
        => convention == NamingConvention.PascalCase
            ? NamingHelper.SuggestPascalCase(name)
            : NamingHelper.SuggestCamelCase(name);

    /// <summary>Tries to read and parse a casing convention from a single editorconfig key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="key">The editorconfig key.</param>
    /// <param name="convention">The parsed convention when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the key was present and recognized.</returns>
    private static bool TryRead(AnalyzerConfigOptions options, string key, out NamingConvention convention)
    {
        convention = NamingConvention.PascalCase;
        if (!options.TryGetValue(key, out var value))
        {
            return false;
        }

        if (string.Equals(value, "pascal_case", StringComparison.OrdinalIgnoreCase))
        {
            convention = NamingConvention.PascalCase;
            return true;
        }

        if (!string.Equals(value, "camel_case", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        convention = NamingConvention.CamelCase;
        return true;
    }
}
