// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Answers whether a syntax tree may use a language version that the compiler this assembly was
/// built against has no enum member for, so a rule can suggest newer syntax without hard-coding a
/// version number.
/// </summary>
/// <remarks>
/// <para>
/// Which versions <c>LanguageVersion</c> names depends on the slot: the roslyn4.8 floor stops at
/// C# 12, roslyn4.14 stops at C# 13, and none of the published compilers name C# 15. Writing the
/// numbers by hand instead is how a gate silently ends up wrong — C# 6 is 6 and C# 7 is 7, but C# 7.1
/// onwards are 701, 800, 900, so a plausible-looking 600 or 700 excludes the very versions it means
/// to include.
/// </para>
/// <para>
/// <see cref="LanguageVersionFacts"/> resolves the name against the compiler that loaded the
/// analyzer, so an older build still recognises a newer version when it runs on a newer host, and
/// reports it unavailable when it does not — which is right, because that host could not compile the
/// syntax either. Versions the floor already names are used directly and are not repeated here.
/// </para>
/// </remarks>
internal static class LanguageVersions
{
    /// <summary>C# 13 as the host names it, or <see langword="null"/> where it has no such version.</summary>
    private static readonly LanguageVersion? CSharp13Version = Resolve("13.0");

    /// <summary>C# 14 as the host names it, or <see langword="null"/> where it has no such version.</summary>
    private static readonly LanguageVersion? CSharp14Version = Resolve("14.0");

    /// <summary>C# 15 as the host names it, or <see langword="null"/> where it has no such version.</summary>
    private static readonly LanguageVersion? CSharp15Version = Resolve("15.0");

    /// <summary>Gets whether a node's tree may use C# 13 syntax.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns><see langword="true"/> when the tree was parsed as C# 13 or later.</returns>
    public static bool SupportsCSharp13(SyntaxNode node) => IsAtLeast(node, CSharp13Version);

    /// <summary>Gets whether a node's tree may use C# 14 syntax.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns><see langword="true"/> when the tree was parsed as C# 14 or later.</returns>
    public static bool SupportsCSharp14(SyntaxNode node) => IsAtLeast(node, CSharp14Version);

    /// <summary>Gets whether a node's tree may use C# 15 syntax.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns><see langword="true"/> when the tree was parsed as C# 15 or later.</returns>
    public static bool SupportsCSharp15(SyntaxNode node) => IsAtLeast(node, CSharp15Version);

    /// <summary>Resolves a version by the name the compiler and project files spell it with.</summary>
    /// <param name="text">The version text, such as <c>15.0</c>.</param>
    /// <returns>The version, or <see langword="null"/> when the host compiler has no such version.</returns>
    private static LanguageVersion? Resolve(string text)
        => LanguageVersionFacts.TryParse(text, out var version) ? version : null;

    /// <summary>Gets whether a node's tree was parsed with at least the supplied language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The required version, or <see langword="null"/> when the host cannot name it.</param>
    /// <returns><see langword="true"/> when the tree supports that version.</returns>
    /// <remarks>
    /// <c>preview</c> satisfies every check: it parses the newest syntax the host knows, including a
    /// version the host has no numbered member for. <c>latest</c> and <c>latestMajor</c> never reach
    /// here, the compiler having resolved them to a concrete version before the options are read.
    /// </remarks>
    private static bool IsAtLeast(SyntaxNode node, LanguageVersion? version)
    {
        if (node.SyntaxTree.Options is not CSharpParseOptions options)
        {
            return false;
        }

        return options.LanguageVersion == LanguageVersion.Preview
            || (version is { } required && options.LanguageVersion >= required);
    }
}
