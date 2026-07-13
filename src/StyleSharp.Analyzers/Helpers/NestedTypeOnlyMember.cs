// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>One private member of a type, and what the type and its nested types do with it (SST1498).</summary>
internal sealed class NestedTypeOnlyMember
{
    /// <summary>Initializes a new instance of the <see cref="NestedTypeOnlyMember"/> class.</summary>
    /// <param name="symbol">The member's symbol.</param>
    /// <param name="declaration">The declaration that would move.</param>
    /// <param name="identifier">The identifier the diagnostic is reported on.</param>
    public NestedTypeOnlyMember(ISymbol symbol, MemberDeclarationSyntax declaration, SyntaxToken identifier)
    {
        Symbol = symbol;
        Declaration = declaration;
        Identifier = identifier;
    }

    /// <summary>Gets the member's symbol.</summary>
    public ISymbol Symbol { get; }

    /// <summary>Gets the declaration that would move into the nested type.</summary>
    /// <remarks>A field declaration can declare several variables, so this is not one member per declaration.</remarks>
    public MemberDeclarationSyntax Declaration { get; }

    /// <summary>Gets the identifier the diagnostic is reported on.</summary>
    public SyntaxToken Identifier { get; }

    /// <summary>Gets the only nested type that uses the member, once one has been seen.</summary>
    public BaseTypeDeclarationSyntax? NestedUser { get; private set; }

    /// <summary>Gets or sets a value indicating whether the type that declares the member also uses it.</summary>
    public bool UsedByOuterType { get; set; }

    /// <summary>Gets a value indicating whether more than one nested type uses the member.</summary>
    /// <remarks>Then there is no single place to move it to, and the rule says nothing.</remarks>
    public bool UsedByManyNestedTypes { get; private set; }

    /// <summary>Gets a value indicating whether every nested use writes the member's name on its own.</summary>
    /// <remarks>
    /// A use written as <c>Outer.Helper()</c> names the type the member is moving out of, so moving it would
    /// leave that reference behind; only an unqualified use still binds once the member has moved.
    /// </remarks>
    public bool NestedUsesAreUnqualified { get; private set; } = true;

    /// <summary>Gets a value indicating whether one nested type has taken the member over entirely.</summary>
    public bool IsUsedOnlyByOneNestedType => NestedUser is not null && !UsedByManyNestedTypes && !UsedByOuterType;

    /// <summary>Records one use of the member from inside a nested type.</summary>
    /// <param name="nested">The nested type the use was found in.</param>
    /// <param name="qualified">Whether the use named a receiver.</param>
    public void AddNestedUse(BaseTypeDeclarationSyntax nested, bool qualified)
    {
        if (NestedUser is null)
        {
            NestedUser = nested;
        }
        else if (NestedUser != nested)
        {
            UsedByManyNestedTypes = true;
        }

        NestedUsesAreUnqualified &= !qualified;
    }
}
