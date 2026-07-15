// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the ordering (SST12xx) diagnostic descriptors.
/// Members are ordered by the default precedence: kind, then accessibility,
/// then constant, then static, then readonly.
/// </summary>
internal static partial class OrderingRules
{
    /// <summary>SST1201 — members should be ordered by kind.</summary>
    public static readonly DiagnosticDescriptor OrderByKind = Create(
        "SST1201",
        "Elements should be ordered by kind",
        "'{0}' is out of order: members should appear by kind (fields, constructors, …, methods, then nested structs, classes, records, unions)",
        "Members appear grouped by kind in the conventional order.");

    /// <summary>SST1202 — members should be ordered by accessibility.</summary>
    public static readonly DiagnosticDescriptor OrderByAccess = Create(
        "SST1202",
        "Elements should be ordered by access",
        "'{0}' is out of order: more accessible members should appear before less accessible ones",
        "Within a kind, members appear from most to least accessible.");

    /// <summary>SST1203 — constants should appear before fields.</summary>
    public static readonly DiagnosticDescriptor ConstantsBeforeFields = Create(
        "SST1203",
        "Constants should appear before fields",
        "Constant '{0}' should appear before the non-constant fields",
        "Constant fields appear before non-constant fields of the same accessibility.");

    /// <summary>SST1204 — static members should appear before instance members.</summary>
    public static readonly DiagnosticDescriptor StaticBeforeInstance = Create(
        "SST1204",
        "Static members should appear before instance members",
        "Static member '{0}' should appear before the instance members",
        "Static members appear before instance members of the same kind and accessibility.");

    /// <summary>SST1214 — static readonly fields should appear before static non-readonly fields.</summary>
    public static readonly DiagnosticDescriptor ReadonlyBeforeNonReadonly = Create(
        "SST1214",
        "Static readonly fields should appear before static non-readonly fields",
        "Static readonly field '{0}' should appear before the static non-readonly fields",
        "Static readonly fields appear before static non-readonly fields of the same accessibility.");

    /// <summary>SST1215 — instance readonly fields should appear before instance non-readonly fields.</summary>
    public static readonly DiagnosticDescriptor InstanceReadonlyBeforeNonReadonly = Create(
        "SST1215",
        "Instance readonly fields should appear before instance non-readonly fields",
        "Instance readonly field '{0}' should appear before the instance non-readonly fields",
        "Instance readonly fields appear before instance non-readonly fields of the same accessibility.");

    /// <summary>SST1200 — using directives should be placed outside the namespace.</summary>
    public static readonly DiagnosticDescriptor UsingDirectivesPlacement = Create(
        "SST1200",
        "Using directives should be placed outside the namespace",
        "Move the using directive outside the namespace declaration",
        "Using directives are placed outside the namespace declaration.");

    /// <summary>SST1205 — partial elements should declare an access modifier.</summary>
    public static readonly DiagnosticDescriptor PartialElementAccess = Create(
        "SST1205",
        "Partial elements should declare an access modifier",
        "Partial '{0}' should declare an explicit access modifier",
        "Partial types and methods declare their accessibility explicitly.");

    /// <summary>SST1206 — declaration keywords should follow the standard order.</summary>
    public static readonly DiagnosticDescriptor DeclarationKeywordOrder = Create(
        "SST1206",
        "Declaration keywords should follow the standard order",
        "The modifier '{0}' is out of order",
        "Modifiers appear in the conventional order: access, then static, then the remaining keywords.");

    /// <summary>SST1207 — <c>protected</c> should come before <c>internal</c>.</summary>
    public static readonly DiagnosticDescriptor ProtectedBeforeInternal = Create(
        "SST1207",
        "Protected should come before internal",
        "'protected' should appear before 'internal'",
        "In a combined access modifier, 'protected' precedes 'internal'.");

    /// <summary>SST1208 — System using directives should appear before other usings.</summary>
    public static readonly DiagnosticDescriptor SystemUsingsFirst = Create(
        "SST1208",
        "System using directives should appear before other usings",
        "Move the System using directive before the non-System using directives",
        "Using directives in the 'System' namespace appear before other using directives.");

    /// <summary>SST1209 — using alias directives should appear after other usings.</summary>
    public static readonly DiagnosticDescriptor AliasUsingsLast = Create(
        "SST1209",
        "Using alias directives should appear after other usings",
        "Move the using alias directive after the regular and static using directives",
        "Using alias directives appear after all regular and static using directives.");

    /// <summary>SST1210 — regular using directives should be ordered alphabetically.</summary>
    public static readonly DiagnosticDescriptor RegularUsingsAlphabetical = Create(
        "SST1210",
        "Using directives should be ordered alphabetically by namespace",
        "Order the using directive alphabetically by namespace",
        "Regular using directives are ordered alphabetically by namespace.");

    /// <summary>SST1211 — using alias directives should be ordered alphabetically by alias.</summary>
    public static readonly DiagnosticDescriptor AliasUsingsAlphabetical = Create(
        "SST1211",
        "Using alias directives should be ordered alphabetically by alias name",
        "Order the using alias directive alphabetically by alias name",
        "Using alias directives are ordered alphabetically by alias name.");

    /// <summary>SST1212 — property accessors should be ordered with get first.</summary>
    public static readonly DiagnosticDescriptor PropertyAccessorOrder = Create(
        "SST1212",
        "Property accessors should be ordered with get first",
        "The get accessor should appear before the set/init accessor",
        "A get accessor appears before the set or init accessor.");

    /// <summary>SST1213 — event accessors should be ordered with add first.</summary>
    public static readonly DiagnosticDescriptor EventAccessorOrder = Create(
        "SST1213",
        "Event accessors should be ordered with add first",
        "The add accessor should appear before the remove accessor",
        "An add accessor appears before the remove accessor.");

    /// <summary>SST1216 — using static directives should be placed after regular usings and before aliases.</summary>
    public static readonly DiagnosticDescriptor StaticUsingsPlacement = Create(
        "SST1216",
        "Using static directives should appear after regular usings and before aliases",
        "Move the using static directive after the regular usings and before the alias usings",
        "Using static directives appear after regular using directives and before using alias directives.");

    /// <summary>SST1217 — using static directives should be ordered alphabetically.</summary>
    public static readonly DiagnosticDescriptor StaticUsingsAlphabetical = Create(
        "SST1217",
        "Using static directives should be ordered alphabetically",
        "Order the using static directive alphabetically",
        "Using static directives are ordered alphabetically by namespace.");

    /// <summary>SST1218 — a method's overloads are split apart by other members.</summary>
    public static readonly DiagnosticDescriptor OverloadsGrouped = Create(
        "SST1218",
        "Method overloads should be grouped together",
        "The overloads of '{0}' are separated by other members; keep them together",
        OverloadsGroupedDescription);

    /// <summary>The OverloadsGrouped rule description.</summary>
    private const string OverloadsGroupedDescription =
        "Overloads are one idea with several entry points. Scattering them through a type means a reader who found one has no reason to "
        + "suspect the others exist, and a change to the family gets applied to the copy they happened to land on.";

    /// <summary>Creates a Warning-severity Ordering descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format (the offending member name is argument 0).</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Ordering", description);
}
