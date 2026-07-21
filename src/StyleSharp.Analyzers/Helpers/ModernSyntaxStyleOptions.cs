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

    /// <summary>Reads the configured infinite-loop style, defaulting to <see cref="InfiniteLoopStyle.While"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    public static InfiniteLoopStyle ReadInfiniteLoopStyle(AnalyzerConfigOptions options)
        => Read(options, InfiniteLoopStyleSpecificKey, InfiniteLoopStyleGeneralKey) switch
        {
            "for" => InfiniteLoopStyle.For,
            "while" => InfiniteLoopStyle.While,
            _ => InfiniteLoopStyle.While,
        };

    /// <summary>Reads the configured object-creation parentheses style, defaulting to <see cref="ObjectCreationParenthesesStyle.Omit"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    public static ObjectCreationParenthesesStyle ReadObjectCreationParentheses(AnalyzerConfigOptions options)
        => Read(options, ObjectCreationParenthesesSpecificKey, ObjectCreationParenthesesGeneralKey) switch
        {
            "include" => ObjectCreationParenthesesStyle.Include,
            "omit" => ObjectCreationParenthesesStyle.Omit,
            _ => ObjectCreationParenthesesStyle.Omit,
        };

    /// <summary>Reads the configured conditional-condition parentheses style, defaulting to <see cref="ConditionalConditionParenthesesStyle.OmitWhenSingleToken"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    public static ConditionalConditionParenthesesStyle ReadConditionalConditionParentheses(AnalyzerConfigOptions options)
        => Read(options, ConditionalConditionParenthesesSpecificKey, ConditionalConditionParenthesesGeneralKey) switch
        {
            "include" => ConditionalConditionParenthesesStyle.Include,
            "omit_when_single_token" => ConditionalConditionParenthesesStyle.OmitWhenSingleToken,
            _ => ConditionalConditionParenthesesStyle.OmitWhenSingleToken,
        };

    /// <summary>Reads the configured array-creation type style, defaulting to <see cref="ArrayCreationTypeStyle.ImplicitWhenObvious"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    public static ArrayCreationTypeStyle ReadArrayCreationTypeStyle(AnalyzerConfigOptions options)
        => Read(options, ArrayCreationTypeStyleSpecificKey, ArrayCreationTypeStyleGeneralKey) switch
        {
            "explicit" => ArrayCreationTypeStyle.Explicit,
            "implicit" => ArrayCreationTypeStyle.Implicit,
            "implicit_when_obvious" => ArrayCreationTypeStyle.ImplicitWhenObvious,
            _ => ArrayCreationTypeStyle.ImplicitWhenObvious,
        };

    /// <summary>Reads the configured var style, defaulting to <see cref="UseVarStyle.WhenObvious"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    public static UseVarStyle ReadUseVar(AnalyzerConfigOptions options)
        => Read(options, UseVarSpecificKey, UseVarGeneralKey) switch
        {
            "always" => UseVarStyle.Always,
            "never" => UseVarStyle.Never,
            "when_obvious" => UseVarStyle.WhenObvious,
            _ => UseVarStyle.WhenObvious,
        };

    /// <summary>Reads the configured Flags-enum value style, defaulting to <see cref="EnumFlagValueStyle.Shift"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    public static EnumFlagValueStyle ReadEnumFlagValueStyle(AnalyzerConfigOptions options)
        => Read(options, EnumFlagValueStyleSpecificKey, EnumFlagValueStyleGeneralKey) switch
        {
            "decimal" => EnumFlagValueStyle.Decimal,
            "shift" => EnumFlagValueStyle.Shift,
            _ => EnumFlagValueStyle.Shift,
        };

    /// <summary>Reads the raw value of an option, preferring the rule-specific key, lowercased and trimmed.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="specificKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The normalized value, or <see langword="null"/> when neither key carries one.</returns>
    private static string? Read(AnalyzerConfigOptions options, string specificKey, string generalKey)
    {
        if (options.TryGetValue(specificKey, out var value) && value.Length != 0)
        {
            return value.Trim().ToLowerInvariant();
        }

        return options.TryGetValue(generalKey, out value) && value.Length != 0
            ? value.Trim().ToLowerInvariant()
            : null;
    }
}
