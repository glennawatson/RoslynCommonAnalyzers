// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The standard documentation phrasing conventions shared by the analyzer and its
/// code fixes (property accessor prefixes and the constructor summary text).
/// </summary>
internal static class DocumentationConventions
{
    /// <summary>The required leading text of a constructor summary.</summary>
    public const string ConstructorStandardPrefix = "Initializes a new instance of the ";

    /// <summary>
    /// The alternative leading text accepted for an explicitly <c>private</c> constructor —
    /// the "prevent instantiation" phrasing (e.g. for a static-only utility type). A private
    /// constructor may use either this or <see cref="ConstructorStandardPrefix"/>.
    /// </summary>
    public const string PrivateConstructorStandardPrefix = "Prevents a default instance of the ";

    /// <summary>The required leading text of a destructor summary.</summary>
    public const string DestructorStandardPrefix = "Finalizes an instance of the ";

    /// <summary>
    /// Returns the expected leading text for a property summary based on its accessors.
    /// A setter with its own (more restrictive) access modifier is treated as not
    /// externally settable, so the summary reads "Gets " (folding in the rule). An
    /// <c>init</c> accessor is likewise treated as read-oriented — matching the analyzer's
    /// the rule, an init-only property's summary begins with "Gets ", not "Gets or sets ".
    /// </summary>
    /// <param name="property">The property declaration.</param>
    /// <returns>"Gets ", "Sets ", or "Gets or sets " (with a trailing space).</returns>
    public static string PropertyAccessorPrefix(PropertyDeclarationSyntax property)
    {
        var hasGet = property.ExpressionBody is not null;
        var hasSet = false;

        if (property.AccessorList is { } accessorList)
        {
            foreach (var accessor in accessorList.Accessors)
            {
                if (accessor.Keyword.IsKind(SyntaxKind.GetKeyword))
                {
                    hasGet = true;
                }
                else if (accessor.Keyword.IsKind(SyntaxKind.SetKeyword) && accessor.Modifiers.Count == 0)
                {
                    hasSet = true;
                }
            }
        }

        if (hasGet && hasSet)
        {
            return "Gets or sets ";
        }

        return hasSet ? "Sets " : "Gets ";
    }

    /// <summary>Returns whether a property has a set or init accessor with explicit restricted accessibility.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> when a write accessor declares its own accessibility.</returns>
    public static bool HasRestrictedWriteAccessor(PropertyDeclarationSyntax property)
    {
        if (property.ExplicitInterfaceSpecifier is not null || property.AccessorList is not { } accessorList)
        {
            return false;
        }

        for (var i = 0; i < accessorList.Accessors.Count; i++)
        {
            var accessor = accessorList.Accessors[i];
            if ((accessor.Keyword.IsKind(SyntaxKind.SetKeyword) || accessor.Keyword.IsKind(SyntaxKind.InitKeyword))
                && accessor.Modifiers.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the standard constructor summary text referencing <paramref name="type"/>.</summary>
    /// <param name="type">The declaring type.</param>
    /// <returns>The standard summary inner text.</returns>
    public static string ConstructorStandardSummary(TypeDeclarationSyntax type)
        => ConstructorStandardPrefix + Reference(type);

    /// <summary>Returns the standard destructor summary text referencing <paramref name="type"/>.</summary>
    /// <param name="type">The declaring type.</param>
    /// <returns>The standard summary inner text.</returns>
    public static string DestructorStandardSummary(TypeDeclarationSyntax type)
        => DestructorStandardPrefix + Reference(type);

    /// <summary>Builds the <c>&lt;see cref="Type"/&gt; class.</c> / <c>struct.</c> reference for a type.</summary>
    /// <param name="type">The declaring type.</param>
    /// <returns>The reference text.</returns>
    private static string Reference(TypeDeclarationSyntax type)
    {
        var isStruct = type is StructDeclarationSyntax
            || (type is RecordDeclarationSyntax record && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword));
        return "<see cref=\"" + type.Identifier.ValueText + "\"/> " + (isStruct ? "struct." : "class.");
    }
}
