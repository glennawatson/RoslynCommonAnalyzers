// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The resolved PSH1007 settings for one syntax tree.</summary>
/// <param name="MinimumSize">The smallest estimated struct size that is worth an <c>in</c>.</param>
/// <param name="ExcludedTypes">Additional type names the rule leaves alone, or an empty array.</param>
/// <param name="IncludePublicApi">Whether externally visible members are reported.</param>
internal readonly record struct InParameterOptions(
    int MinimumSize,
    string[] ExcludedTypes,
    bool IncludePublicApi)
{
    /// <summary>The default minimum size, which is Microsoft's "three words or less is negligible" guidance.</summary>
    public const int DefaultMinimumSize = 32;

    /// <summary>
    /// The smallest size the rule will ever report at, whatever the configuration says.
    /// </summary>
    /// <remarks>
    /// A struct of 16 bytes or less is passed in registers by the System V (Linux/macOS) and ARM64 ABIs.
    /// Forcing it behind an <c>in</c> makes the caller spill it to the stack and pass a pointer, which is
    /// strictly worse. Since the same assembly runs on every ABI, this floor is not configurable away.
    /// </remarks>
    public const int MinimumConfigurableSize = 17;

    /// <summary>The rule-specific size key.</summary>
    private const string SizeRuleKey = "performancesharp.PSH1007.in_parameter_minimum_size";

    /// <summary>The project-wide size key.</summary>
    private const string SizeGeneralKey = "performancesharp.in_parameter_minimum_size";

    /// <summary>The rule-specific excluded-types key.</summary>
    private const string ExcludedRuleKey = "performancesharp.PSH1007.in_parameter_excluded_types";

    /// <summary>The project-wide excluded-types key.</summary>
    private const string ExcludedGeneralKey = "performancesharp.in_parameter_excluded_types";

    /// <summary>The rule-specific public-API key.</summary>
    private const string PublicApiRuleKey = "performancesharp.PSH1007.in_parameter_include_public_api";

    /// <summary>The project-wide public-API key.</summary>
    private const string PublicApiGeneralKey = "performancesharp.in_parameter_include_public_api";

    /// <summary>The types the rule never suggests <c>in</c> for, whatever their size.</summary>
    /// <remarks>
    /// <para>
    /// Every SIMD type here is a <c>readonly struct</c> larger than the floor, so a size-only rule would
    /// happily flag <c>Vector256&lt;T&gt;</c> and <c>Vector512&lt;T&gt;</c>. The whole of the BCL's vector
    /// surface — including the 64-byte <c>Matrix4x4</c> — passes these by value and never by <c>in</c>;
    /// they are built to live in registers and to be consumed by code that inlines, where an <c>in</c>
    /// buys nothing.
    /// </para>
    /// <para>
    /// The rest are handles and views: small, cheap, and already a reference to somewhere else. Spans are
    /// excluded structurally as ref structs rather than by name, but are listed for the reader.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> BuiltInExcludedTypes = new(StringComparer.Ordinal)
    {
        "System.Span",
        "System.ReadOnlySpan",
        "System.Memory",
        "System.ReadOnlyMemory",
        "System.Threading.CancellationToken",
        "System.Threading.Tasks.ValueTask",
        "System.Collections.Immutable.ImmutableArray",
        "System.Numerics.Vector",
        "System.Numerics.Vector2",
        "System.Numerics.Vector3",
        "System.Numerics.Vector4",
        "System.Numerics.Quaternion",
        "System.Numerics.Plane",
        "System.Numerics.Matrix3x2",
        "System.Numerics.Matrix4x4",
        "System.Runtime.Intrinsics.Vector64",
        "System.Runtime.Intrinsics.Vector128",
        "System.Runtime.Intrinsics.Vector256",
        "System.Runtime.Intrinsics.Vector512",
    };

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the parameter's tree.</param>
    /// <returns>The resolved settings.</returns>
    public static InParameterOptions Read(AnalyzerConfigOptions options) => new(
        ReadSize(options),
        ReadExcludedTypes(options),
        ReadBool(options, PublicApiRuleKey, PublicApiGeneralKey));

    /// <summary>Returns whether a type is one the rule never suggests <c>in</c> for.</summary>
    /// <param name="type">The parameter's type.</param>
    /// <param name="excludedTypes">The additional configured exclusions.</param>
    /// <returns><see langword="true"/> when the type is excluded.</returns>
    /// <remarks>
    /// Matching ignores generic arity and accepts either the full name or the bare name, so
    /// <c>Vector256</c> and <c>System.Runtime.Intrinsics.Vector256</c> both name the same type and a
    /// <c>using</c> alias cannot defeat the match.
    /// </remarks>
    public static bool IsExcluded(INamedTypeSymbol type, string[] excludedTypes)
    {
        var name = type.Name;
        var fullName = GetFullName(type);
        if (BuiltInExcludedTypes.Contains(fullName))
        {
            return true;
        }

        for (var i = 0; i < excludedTypes.Length; i++)
        {
            var excluded = excludedTypes[i];
            if (string.Equals(excluded, fullName, StringComparison.Ordinal)
                || string.Equals(excluded, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds a type's namespace-qualified name, without generic arity.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The full name, such as <c>System.Runtime.Intrinsics.Vector256</c>.</returns>
    private static string GetFullName(INamedTypeSymbol type)
        => type.ContainingNamespace is { IsGlobalNamespace: false } containing
            ? containing.ToDisplayString() + "." + type.Name
            : type.Name;

    /// <summary>Reads the minimum size, never returning a value below the ABI floor.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <returns>The configured size, clamped to the floor.</returns>
    private static int ReadSize(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(SizeRuleKey, out var value) && !options.TryGetValue(SizeGeneralKey, out value))
        {
            return DefaultMinimumSize;
        }

        return int.TryParse(value, out var parsed) && parsed > 0
            ? Math.Max(parsed, MinimumConfigurableSize)
            : DefaultMinimumSize;
    }

    /// <summary>Reads the configured type exclusions.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <returns>The parsed type names, or an empty array.</returns>
    private static string[] ReadExcludedTypes(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(ExcludedRuleKey, out var value) && !options.TryGetValue(ExcludedGeneralKey, out value))
        {
            return [];
        }

        var parts = value.Split(',');
        var parsed = new string[parts.Length];
        var count = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var trimmed = parts[i].Trim();
            if (trimmed.Length > 0)
            {
                parsed[count++] = trimmed;
            }
        }

        if (count == parts.Length)
        {
            return parsed;
        }

        var result = new string[count];
        Array.Copy(parsed, result, count);
        return result;
    }

    /// <summary>Reads a boolean setting that defaults to false, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The configured value, or <see langword="false"/>.</returns>
    private static bool ReadBool(AnalyzerConfigOptions options, string ruleKey, string generalKey)
    {
        if (options.TryGetValue(ruleKey, out var value) && bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return options.TryGetValue(generalKey, out value) && bool.TryParse(value, out parsed) && parsed;
    }
}
