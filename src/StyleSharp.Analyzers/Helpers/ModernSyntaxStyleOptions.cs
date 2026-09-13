// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the <c>.editorconfig</c> style choices for the configurable Modern-Syntax normalization rules
/// (SST2267–SST2272). Each option supports the CA key convention: a rule-specific key
/// (<c>stylesharp.SST22xx.&lt;option&gt;</c>) that overrides the project-wide key
/// (<c>stylesharp.&lt;option&gt;</c>). An unset or unrecognized value falls back to the documented default.
/// The value is read only after a syntactic candidate is found, so a source with no candidates pays nothing.
/// </summary>
internal static class ModernSyntaxStyleOptions
{
    /// <summary>The project-wide infinite-loop style key.</summary>
    public const string InfiniteLoopStyleGeneralKey = "stylesharp.infinite_loop_style";

    /// <summary>The project-wide object-creation parentheses key.</summary>
    public const string ObjectCreationParenthesesGeneralKey = "stylesharp.object_creation_parentheses";

    /// <summary>The project-wide conditional-condition parentheses key.</summary>
    public const string ConditionalConditionParenthesesGeneralKey = "stylesharp.conditional_condition_parentheses";

    /// <summary>The project-wide array-creation type-style key.</summary>
    public const string ArrayCreationTypeStyleGeneralKey = "stylesharp.array_creation_type_style";

    /// <summary>The project-wide var-style key.</summary>
    public const string UseVarGeneralKey = "stylesharp.use_var";

    /// <summary>The project-wide Flags-enum value-style key.</summary>
    public const string EnumFlagValueStyleGeneralKey = "stylesharp.enum_flag_value_style";

    /// <summary>The project-wide namespace declaration-style key.</summary>
    public const string NamespaceDeclarationStyleGeneralKey = "stylesharp.namespace_declaration_style";

    /// <summary>The rule-specific infinite-loop style key.</summary>
    private const string InfiniteLoopStyleSpecificKey = "stylesharp.SST2267.infinite_loop_style";

    /// <summary>The rule-specific object-creation parentheses key.</summary>
    private const string ObjectCreationParenthesesSpecificKey = "stylesharp.SST2268.object_creation_parentheses";

    /// <summary>The rule-specific conditional-condition parentheses key.</summary>
    private const string ConditionalConditionParenthesesSpecificKey = "stylesharp.SST2269.conditional_condition_parentheses";

    /// <summary>The rule-specific array-creation type-style key.</summary>
    private const string ArrayCreationTypeStyleSpecificKey = "stylesharp.SST2270.array_creation_type_style";

    /// <summary>The rule-specific var-style key.</summary>
    private const string UseVarSpecificKey = "stylesharp.SST2271.use_var";

    /// <summary>The rule-specific Flags-enum value-style key.</summary>
    private const string EnumFlagValueStyleSpecificKey = "stylesharp.SST2272.enum_flag_value_style";

    /// <summary>The rule-specific namespace declaration-style key.</summary>
    private const string NamespaceDeclarationStyleSpecificKey = "stylesharp.SST2237.namespace_declaration_style";

    /// <summary>Reads the configured infinite-loop style, defaulting to <see cref="InfiniteLoopStyle.While"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    internal static InfiniteLoopStyle ReadInfiniteLoopStyle(AnalyzerConfigOptions options) =>
        Read(options, InfiniteLoopStyleSpecificKey, InfiniteLoopStyleGeneralKey) switch
        {
            var value when IsValue(value, "for") => InfiniteLoopStyle.For,
            var value when IsValue(value, "while") => InfiniteLoopStyle.While,
            _ => InfiniteLoopStyle.While,
        };

    /// <summary>Reads the configured object-creation parentheses style, defaulting to <see cref="ObjectCreationParenthesesStyle.Omit"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    internal static ObjectCreationParenthesesStyle ReadObjectCreationParentheses(AnalyzerConfigOptions options) =>
        Read(options, ObjectCreationParenthesesSpecificKey, ObjectCreationParenthesesGeneralKey) switch
        {
            var value when IsValue(value, "include") => ObjectCreationParenthesesStyle.Include,
            var value when IsValue(value, "omit") => ObjectCreationParenthesesStyle.Omit,
            _ => ObjectCreationParenthesesStyle.Omit,
        };

    /// <summary>Reads the configured conditional-condition parentheses style, defaulting to <see cref="ConditionalConditionParenthesesStyle.OmitWhenSingleToken"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    internal static ConditionalConditionParenthesesStyle ReadConditionalConditionParentheses(AnalyzerConfigOptions options) =>
        Read(options, ConditionalConditionParenthesesSpecificKey, ConditionalConditionParenthesesGeneralKey) switch
        {
            var value when IsValue(value, "include") => ConditionalConditionParenthesesStyle.Include,
            var value when IsValue(value, "omit_when_single_token") => ConditionalConditionParenthesesStyle.OmitWhenSingleToken,
            _ => ConditionalConditionParenthesesStyle.OmitWhenSingleToken,
        };

    /// <summary>Reads the configured array-creation type style, defaulting to <see cref="ArrayCreationTypeStyle.ImplicitWhenObvious"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    internal static ArrayCreationTypeStyle ReadArrayCreationTypeStyle(AnalyzerConfigOptions options) =>
        Read(options, ArrayCreationTypeStyleSpecificKey, ArrayCreationTypeStyleGeneralKey) switch
        {
            var value when IsValue(value, "explicit") => ArrayCreationTypeStyle.Explicit,
            var value when IsValue(value, "implicit") => ArrayCreationTypeStyle.Implicit,
            var value when IsValue(value, "implicit_when_obvious") => ArrayCreationTypeStyle.ImplicitWhenObvious,
            _ => ArrayCreationTypeStyle.ImplicitWhenObvious,
        };

    /// <summary>Reads the configured var style, defaulting to <see cref="UseVarStyle.WhenObvious"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    internal static UseVarStyle ReadUseVar(AnalyzerConfigOptions options) =>
        Read(options, UseVarSpecificKey, UseVarGeneralKey) switch
        {
            var value when IsValue(value, "always") => UseVarStyle.Always,
            var value when IsValue(value, "never") => UseVarStyle.Never,
            var value when IsValue(value, "when_obvious") => UseVarStyle.WhenObvious,
            _ => UseVarStyle.WhenObvious,
        };

    /// <summary>Reads the configured Flags-enum value style, defaulting to <see cref="EnumFlagValueStyle.Shift"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    internal static EnumFlagValueStyle ReadEnumFlagValueStyle(AnalyzerConfigOptions options) =>
        Read(options, EnumFlagValueStyleSpecificKey, EnumFlagValueStyleGeneralKey) switch
        {
            var value when IsValue(value, "decimal") => EnumFlagValueStyle.Decimal,
            var value when IsValue(value, "shift") => EnumFlagValueStyle.Shift,
            _ => EnumFlagValueStyle.Shift,
        };

    /// <summary>Reads the configured namespace declaration style, defaulting to <see cref="NamespaceDeclarationStyle.FileScoped"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    internal static NamespaceDeclarationStyle ReadNamespaceDeclarationStyle(AnalyzerConfigOptions options) =>
        Read(options, NamespaceDeclarationStyleSpecificKey, NamespaceDeclarationStyleGeneralKey) switch
        {
            var value when IsValue(value, "block_scoped") => NamespaceDeclarationStyle.BlockScoped,
            var value when IsValue(value, "file_scoped") => NamespaceDeclarationStyle.FileScoped,
            _ => NamespaceDeclarationStyle.FileScoped,
        };

    /// <summary>Reads the raw value of an option, preferring the rule-specific key and excluding surrounding whitespace.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="specificKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The trimmed value, or an empty span when neither key carries one.</returns>
    private static ReadOnlySpan<char> Read(AnalyzerConfigOptions options, string specificKey, string generalKey)
    {
        if ((!options.TryGetValue(specificKey, out var value) || value.Length == 0)
            && (!options.TryGetValue(generalKey, out value) || value.Length == 0))
        {
            return default;
        }

        var start = 0;
        var end = value.Length;
        while (start < end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return value.AsSpan(start, end - start);
    }

    /// <summary>Compares an option with a lowercase ASCII keyword using the original invariant-lowercase semantics.</summary>
    /// <param name="value">The trimmed option value.</param>
    /// <param name="keyword">The lowercase ASCII keyword.</param>
    /// <returns>Whether invariant lowercasing would produce the keyword.</returns>
    private static bool IsValue(ReadOnlySpan<char> value, string keyword)
    {
        if (value.Length != keyword.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (char.ToLowerInvariant(value[i]) != keyword[i])
            {
                return false;
            }
        }

        return true;
    }
}
