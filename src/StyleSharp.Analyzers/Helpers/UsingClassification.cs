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
    {
        if (directive.Alias is { } alias)
        {
            return alias.Name.Identifier.ValueText;
        }

        var name = directive.Name;
        return name is IdentifierNameSyntax identifier
            ? identifier.Identifier.ValueText
            : name?.ToString() ?? string.Empty;
    }

    /// <summary>Compares two using directives by their alphabetical sort key without allocating for common name shapes.</summary>
    /// <param name="left">The first directive.</param>
    /// <param name="right">The second directive.</param>
    /// <returns>A negative, zero, or positive value according to canonical alphabetical ordering.</returns>
    public static int CompareSortKey(UsingDirectiveSyntax left, UsingDirectiveSyntax right)
    {
        if (left.Alias is { } leftAlias && right.Alias is { } rightAlias)
        {
            return string.CompareOrdinal(leftAlias.Name.Identifier.ValueText, rightAlias.Name.Identifier.ValueText);
        }

        return left.Name is { } leftName && right.Name is { } rightName && IsIdentifierOrQualified(leftName) && IsIdentifierOrQualified(rightName)
            ? CompareIdentifierOrQualifiedName(leftName, rightName)
            : string.CompareOrdinal(SortKey(left), SortKey(right));
    }

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

        return CompareSortKey(left, right);
    }

    /// <summary>Returns whether a name is composed only of identifiers separated by dots.</summary>
    /// <param name="name">The name syntax.</param>
    /// <returns><see langword="true"/> when the common fast comparison path applies.</returns>
    private static bool IsIdentifierOrQualified(NameSyntax name)
        => name is IdentifierNameSyntax or QualifiedNameSyntax;

    /// <summary>
    /// Compares identifier-or-qualified names by their dotted segments from the left, without allocating.
    /// Names of differing depth are aligned to their shared leading length first (the syntax tree is
    /// left-associative, so the last segment is the outermost node); when every shared segment is equal,
    /// the shorter name sorts first.
    /// </summary>
    /// <param name="left">The left name.</param>
    /// <param name="right">The right name.</param>
    /// <returns>A negative, zero, or positive value according to ordinal segment ordering.</returns>
    private static int CompareIdentifierOrQualifiedName(NameSyntax left, NameSyntax right)
    {
        var leftDepth = SegmentCount(left);
        var rightDepth = SegmentCount(right);

        if (leftDepth > rightDepth)
        {
            var compare = CompareSameDepth(PrefixOfLength(left, leftDepth, rightDepth), right);
            return compare != 0 ? compare : 1;
        }

        if (rightDepth > leftDepth)
        {
            var compare = CompareSameDepth(left, PrefixOfLength(right, rightDepth, leftDepth));
            return compare != 0 ? compare : -1;
        }

        return CompareSameDepth(left, right);
    }

    /// <summary>Compares two names known to have the same segment depth, segment-by-segment from the left.</summary>
    /// <param name="left">The left name.</param>
    /// <param name="right">The right name (same depth as <paramref name="left"/>).</param>
    /// <returns>A negative, zero, or positive value according to ordinal segment ordering.</returns>
    private static int CompareSameDepth(NameSyntax left, NameSyntax right)
    {
        if (left is QualifiedNameSyntax leftQualified && right is QualifiedNameSyntax rightQualified)
        {
            var compare = CompareSameDepth(leftQualified.Left, rightQualified.Left);
            return compare != 0
                ? compare
                : string.CompareOrdinal(leftQualified.Right.Identifier.ValueText, rightQualified.Right.Identifier.ValueText);
        }

        return string.CompareOrdinal(
            ((IdentifierNameSyntax)left).Identifier.ValueText,
            ((IdentifierNameSyntax)right).Identifier.ValueText);
    }

    /// <summary>Returns the number of dotted segments in an identifier-or-qualified name.</summary>
    /// <param name="name">The name to measure.</param>
    /// <returns>The segment count (one for a simple identifier).</returns>
    private static int SegmentCount(NameSyntax name)
    {
        var count = 1;
        while (name is QualifiedNameSyntax qualified)
        {
            count++;
            name = qualified.Left;
        }

        return count;
    }

    /// <summary>Returns the leading prefix of a name reduced to the requested number of segments.</summary>
    /// <param name="name">The name to reduce.</param>
    /// <param name="depth">The name's current segment depth.</param>
    /// <param name="length">The desired number of leading segments.</param>
    /// <returns>The prefix name spanning the first <paramref name="length"/> segments.</returns>
    private static NameSyntax PrefixOfLength(NameSyntax name, int depth, int length)
    {
        for (var remaining = depth; remaining > length; remaining--)
        {
            name = ((QualifiedNameSyntax)name).Left;
        }

        return name;
    }

    /// <summary>Returns whether a using target name starts with <c>System</c>.</summary>
    /// <param name="name">The using target name.</param>
    /// <returns><see langword="true"/> when the name is <c>System</c> or one of its children.</returns>
    private static bool StartsWithSystem(NameSyntax name) => name switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "System",
        QualifiedNameSyntax qualified => StartsWithSystem(qualified.Left),
        AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "System" } => true,
        _ => false
    };
}
