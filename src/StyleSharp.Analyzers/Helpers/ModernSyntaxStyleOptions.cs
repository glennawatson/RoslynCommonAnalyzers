// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InfiniteLoopStyle ReadInfiniteLoopStyle(AnalyzerConfigOptions options) =>
        ReadChoice(options, InfiniteLoopStyleSpecificKey, InfiniteLoopStyleGeneralKey, "for", InfiniteLoopStyle.For, InfiniteLoopStyle.While);

    /// <summary>Reads the configured object-creation parentheses style, defaulting to <see cref="ObjectCreationParenthesesStyle.Omit"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ObjectCreationParenthesesStyle ReadObjectCreationParentheses(AnalyzerConfigOptions options) =>
        ReadChoice(
            options,
            ObjectCreationParenthesesSpecificKey,
            ObjectCreationParenthesesGeneralKey,
            "include",
            ObjectCreationParenthesesStyle.Include,
            ObjectCreationParenthesesStyle.Omit);

    /// <summary>Reads the configured conditional-condition parentheses style, defaulting to <see cref="ConditionalConditionParenthesesStyle.OmitWhenSingleToken"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ConditionalConditionParenthesesStyle ReadConditionalConditionParentheses(AnalyzerConfigOptions options) =>
        ReadChoice(
            options,
            ConditionalConditionParenthesesSpecificKey,
            ConditionalConditionParenthesesGeneralKey,
            "include",
            ConditionalConditionParenthesesStyle.Include,
            ConditionalConditionParenthesesStyle.OmitWhenSingleToken);

    /// <summary>Reads the configured array-creation type style, defaulting to <see cref="ArrayCreationTypeStyle.ImplicitWhenObvious"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ArrayCreationTypeStyle ReadArrayCreationTypeStyle(AnalyzerConfigOptions options) =>
        ReadEitherChoice(
            options,
            ArrayCreationTypeStyleSpecificKey,
            ArrayCreationTypeStyleGeneralKey,
            new("explicit", ArrayCreationTypeStyle.Explicit),
            new("implicit", ArrayCreationTypeStyle.Implicit),
            ArrayCreationTypeStyle.ImplicitWhenObvious);

    /// <summary>Reads the configured var style, defaulting to <see cref="UseVarStyle.WhenObvious"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static UseVarStyle ReadUseVar(AnalyzerConfigOptions options) =>
        ReadEitherChoice(
            options,
            UseVarSpecificKey,
            UseVarGeneralKey,
            new("always", UseVarStyle.Always),
            new("never", UseVarStyle.Never),
            UseVarStyle.WhenObvious);

    /// <summary>Reads the configured Flags-enum value style, defaulting to <see cref="EnumFlagValueStyle.Shift"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EnumFlagValueStyle ReadEnumFlagValueStyle(AnalyzerConfigOptions options) =>
        ReadChoice(options, EnumFlagValueStyleSpecificKey, EnumFlagValueStyleGeneralKey, "decimal", EnumFlagValueStyle.Decimal, EnumFlagValueStyle.Shift);

    /// <summary>Reads the configured namespace declaration style, defaulting to <see cref="NamespaceDeclarationStyle.FileScoped"/>.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static NamespaceDeclarationStyle ReadNamespaceDeclarationStyle(AnalyzerConfigOptions options) =>
        ReadChoice(
            options,
            NamespaceDeclarationStyleSpecificKey,
            NamespaceDeclarationStyleGeneralKey,
            "block_scoped",
            NamespaceDeclarationStyle.BlockScoped,
            NamespaceDeclarationStyle.FileScoped);

    /// <summary>Reads an option that names one alternative to its default.</summary>
    /// <typeparam name="TStyle">The style enum.</typeparam>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="specificKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="text">The lowercase option value that selects <paramref name="choice"/>.</param>
    /// <param name="choice">The style the value selects.</param>
    /// <param name="fallback">The style when the option is unset or unrecognized.</param>
    /// <returns>The resolved style.</returns>
    private static TStyle ReadChoice<TStyle>(AnalyzerConfigOptions options, string specificKey, string generalKey, string text, TStyle choice, TStyle fallback) =>
        InvariantText.EqualsLowercase(Read(options, specificKey, generalKey), text) ? choice : fallback;

    /// <summary>Reads an option that names one of two alternatives to its default.</summary>
    /// <typeparam name="TStyle">The style enum.</typeparam>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="specificKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="first">The first alternative and the lowercase value that selects it.</param>
    /// <param name="second">The second alternative and the lowercase value that selects it.</param>
    /// <param name="fallback">The style when the option is unset or unrecognized.</param>
    /// <returns>The resolved style.</returns>
    private static TStyle ReadEitherChoice<TStyle>(
        AnalyzerConfigOptions options,
        string specificKey,
        string generalKey,
        in StyleChoice<TStyle> first,
        in StyleChoice<TStyle> second,
        TStyle fallback)
    {
        var value = Read(options, specificKey, generalKey);
        if (InvariantText.EqualsLowercase(value, first.Text))
        {
            return first.Style;
        }

        return InvariantText.EqualsLowercase(value, second.Text) ? second.Style : fallback;
    }

    /// <summary>Reads the raw value of an option, preferring the rule-specific key and excluding surrounding whitespace.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="specificKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The trimmed value, or an empty span when neither key carries one.</returns>
    private static ReadOnlySpan<char> Read(AnalyzerConfigOptions options, string specificKey, string generalKey) =>
        (options.TryGetValue(specificKey, out var value) && value.Length != 0)
            || (options.TryGetValue(generalKey, out value) && value.Length != 0)
            ? AnalyzerOptionReader.TrimSegment(value, 0, value.Length)
            : default;
}
