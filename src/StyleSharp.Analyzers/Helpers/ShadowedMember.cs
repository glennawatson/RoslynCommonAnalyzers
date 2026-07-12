// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>One field or property a declaration can shadow, as SST1484 needs to describe it.</summary>
/// <param name="IsProperty">Whether the member is a property rather than a field.</param>
/// <param name="IsStatic">Whether the member is static, and so also in scope inside a static member.</param>
/// <param name="IsInherited">Whether the member is declared by a base type rather than by the type itself.</param>
/// <param name="HidesInheritedField">Whether this member is a field of the type that hides an inherited field of the same name.</param>
internal readonly record struct ShadowedMember(
    bool IsProperty,
    bool IsStatic,
    bool IsInherited,
    bool HidesInheritedField)
{
    /// <summary>The wording used for a field a base type declares.</summary>
    public const string InheritedFieldDescription = "inherited field";

    /// <summary>Gets the wording the diagnostic message uses for the shadowed member.</summary>
    public string Description => (IsInherited, IsProperty) switch
    {
        (true, true) => "inherited property",
        (true, false) => InheritedFieldDescription,
        (false, true) => "property",
        _ => "field",
    };

    /// <summary>Returns this member, marked as hiding an inherited field of the same name.</summary>
    /// <returns>The updated member.</returns>
    public ShadowedMember HidingInheritedField() => this with { HidesInheritedField = true };
}
