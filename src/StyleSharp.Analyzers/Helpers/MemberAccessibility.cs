// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads a member's written accessibility: whether its author chose it, and how C# spells it.</summary>
internal static class MemberAccessibility
{
    /// <summary>Returns whether a member's accessibility is written by its author and could be written differently.</summary>
    /// <param name="member">The declared member.</param>
    /// <returns>
    /// <see langword="true"/> for a non-override, non-synthesized ordinary method, property, event, field, or nested type;
    /// an override takes its base member's accessibility.
    /// </returns>
    internal static bool IsAuthored(ISymbol member)
    {
        if (member.IsOverride || member.IsImplicitlyDeclared)
        {
            return false;
        }

        return member switch
        {
            IMethodSymbol method => method.MethodKind == MethodKind.Ordinary,
            IPropertySymbol or IEventSymbol or IFieldSymbol or INamedTypeSymbol => true,
            _ => false,
        };
    }

    /// <summary>Returns the C# keyword spelling of an accessibility, for a diagnostic message.</summary>
    /// <param name="accessibility">The accessibility to spell.</param>
    /// <returns>The keyword text, or the enum name for an accessibility C# cannot write.</returns>
    internal static string Keyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Private => "private",
        _ => accessibility.ToString(),
    };
}
