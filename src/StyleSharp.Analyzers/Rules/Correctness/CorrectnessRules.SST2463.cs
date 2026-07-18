// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2463 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2463 — a derived field's name differs from an inherited field only by case.</summary>
    public static readonly DiagnosticDescriptor InheritedFieldCaseClash = Create(
        "SST2463",
        "A field should not differ from an inherited field only by case",
        "Field '{0}' differs only by case from inherited field '{1}' declared in '{2}', so an unqualified reference to either name compiles and silently uses the wrong storage",
        InheritedFieldCaseClashDescription);

    /// <summary>The InheritedFieldCaseClash rule description.</summary>
    private const string InheritedFieldCaseClashDescription =
        "A derived type declares an instance field whose name matches an accessible field it inherits from a base type when the two "
        + "names are compared without regard to case, yet the names are not identical. The compiler treats them as two separate storage "
        + "locations, so both are reachable by an unqualified name inside the derived type, and a reference that gets the case wrong binds "
        + "to the other field and compiles with no warning — a value written through one is read back as stale or default through the other. "
        + "Only a case-differing clash is reported: an exactly matching name is deliberate hiding and a different concern, a base field the "
        + "derived type cannot see because it is private is never matched, and names that differ by more than case are two ordinary distinct "
        + "fields and are left alone.";
}
