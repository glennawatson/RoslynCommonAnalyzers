// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared helpers that classify a <see cref="UsingDirectiveSyntax"/> for the using-ordering
/// rules. The canonical order is: regular directives (System namespaces first, then
/// alphabetical), then <c>using static</c> directives (alphabetical), then alias directives
/// (alphabetical by alias name).
/// </summary>
internal static class UsingClassification
{
    /// <summary>The group rank of a regular using directive.</summary>
    public const int RegularGroup = 0;

    /// <summary>The group rank of a <c>using static</c> directive.</summary>
    public const int StaticGroup = 1;

    /// <summary>The group rank of a using alias directive.</summary>
    public const int AliasGroup = 2;

    /// <summary>Returns the canonical group rank of a using directive.</summary>
    /// <param name="directive">The using directive.</param>
    /// <returns>The group rank.</returns>
    public static int Group(UsingDirectiveSyntax directive)
    {
        if (directive.Alias is not null)
        {
            return AliasGroup;
        }

        return directive.StaticKeyword.IsKind(SyntaxKind.None) ? RegularGroup : StaticGroup;
    }

    /// <summary>Returns whether a regular using directive targets the <c>System</c> namespace.</summary>
    /// <param name="directive">The using directive.</param>
    /// <returns><see langword="true"/> when the namespace is <c>System</c> or a child of it.</returns>
    public static bool IsSystem(UsingDirectiveSyntax directive)
        => directive.Name is { } name && StartsWithSystem(name);

    /// <summary>Returns the alphabetical sort key of a using directive (alias name, or namespace/type name).</summary>
    /// <param name="directive">The using directive.</param>
    /// <returns>The sort key.</returns>
    public static string SortKey(UsingDirectiveSyntax directive)
        => directive.Alias is { } alias ? alias.Name.Identifier.ValueText : directive.Name?.ToString() ?? string.Empty;

    /// <summary>Compares two using directives by the canonical ordering.</summary>
    /// <param name="left">The first directive.</param>
    /// <param name="right">The second directive.</param>
    /// <returns>A negative, zero, or positive value per the canonical order.</returns>
    public static int Compare(UsingDirectiveSyntax left, UsingDirectiveSyntax right)
    {
        var group = Group(left).CompareTo(Group(right));
        if (group != 0)
        {
            return group;
        }

        if (Group(left) == RegularGroup)
        {
            var system = IsSystem(right).CompareTo(IsSystem(left));
            if (system != 0)
            {
                return system;
            }
        }

        return string.CompareOrdinal(SortKey(left), SortKey(right));
    }

    /// <summary>Returns whether a using target name starts with <c>System</c>.</summary>
    /// <param name="name">The using target name.</param>
    /// <returns><see langword="true"/> when the name is <c>System</c> or one of its children.</returns>
    private static bool StartsWithSystem(NameSyntax name) => name switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "System",
        QualifiedNameSyntax qualified => StartsWithSystem(qualified.Left),
        AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "System" } => true,
        _ => false,
    };
}
