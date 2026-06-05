// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the naming (SST13xx) diagnostic descriptors. Each
/// is created once as <c>static readonly</c> so analyzers share the instances and
/// the shared rename code fix can enumerate every fixable id.
/// </summary>
internal static class NamingRules
{
    /// <summary>SST1300 — types and members should be PascalCase.</summary>
    public static readonly DiagnosticDescriptor ElementPascalCase = Create(
        "SST1300",
        "Element names should be PascalCase",
        "'{0}' should begin with an upper-case letter",
        "Types and non-private members follow the .NET PascalCase convention.");

    /// <summary>SST1302 — interface names should begin with I.</summary>
    public static readonly DiagnosticDescriptor InterfaceI = Create(
        "SST1302",
        "Interface names should begin with I",
        "Interface name '{0}' should begin with 'I'",
        "Interface names should begin with the capital letter 'I' (for example, ICustomer).");

    /// <summary>SST1303 — const names should be PascalCase.</summary>
    public static readonly DiagnosticDescriptor ConstPascalCase = Create(
        "SST1303",
        "Constant names should be PascalCase",
        "Constant '{0}' should begin with an upper-case letter",
        "Constants follow the .NET PascalCase convention.");

    /// <summary>SST1304 — non-private readonly fields should be PascalCase.</summary>
    public static readonly DiagnosticDescriptor NonPrivateReadonlyPascalCase = Create(
        "SST1304",
        "Non-private readonly fields should be PascalCase",
        "Non-private readonly field '{0}' should begin with an upper-case letter",
        "Externally visible readonly fields follow the .NET PascalCase convention.");

    /// <summary>SST1307 — accessible fields should be PascalCase.</summary>
    public static readonly DiagnosticDescriptor AccessibleFieldPascalCase = Create(
        "SST1307",
        "Accessible fields should be PascalCase",
        "Field '{0}' is accessible and should begin with an upper-case letter",
        "Public, internal, and protected fields follow the .NET PascalCase convention.");

    /// <summary>SST1309 — private fields should be _camelCase (runtime convention; inverts StyleCop SA1309).</summary>
    public static readonly DiagnosticDescriptor PrivateFieldUnderscoreCamelCase = Create(
        "SST1309",
        "Private fields should be _camelCase",
        "Private field '{0}' should be named using a leading underscore and camelCase (for example, _value)",
        "Private fields use the .NET runtime convention of a leading underscore followed by camelCase. This is the inverse of StyleCop's SA1309.");

    /// <summary>SST1311 — static readonly fields should be PascalCase.</summary>
    public static readonly DiagnosticDescriptor StaticReadonlyPascalCase = Create(
        "SST1311",
        "Static readonly fields should be PascalCase",
        "Static readonly field '{0}' should begin with an upper-case letter",
        "Static readonly fields follow the .NET PascalCase convention.");

    /// <summary>SST1312 — local variables should be camelCase.</summary>
    public static readonly DiagnosticDescriptor LocalCamelCase = Create(
        "SST1312",
        "Local variables should be camelCase",
        "Local variable '{0}' should begin with a lower-case letter",
        "Local variables follow the .NET camelCase convention.");

    /// <summary>SST1313 — parameters should be camelCase.</summary>
    public static readonly DiagnosticDescriptor ParameterCamelCase = Create(
        "SST1313",
        "Parameters should be camelCase",
        "Parameter '{0}' should begin with a lower-case letter",
        "Parameters follow the .NET camelCase convention.");

    /// <summary>SST1314 — type parameters should begin with T.</summary>
    public static readonly DiagnosticDescriptor TypeParameterT = Create(
        "SST1314",
        "Type parameters should begin with T",
        "Type parameter '{0}' should begin with 'T'",
        "Type parameters begin with the capital letter 'T' (for example, TKey).");

    /// <summary>SST1315 — union member names should use the configured casing (default PascalCase).</summary>
    public static readonly DiagnosticDescriptor UnionMember = Create(
        "SST1315",
        "Union member names should match the configured casing",
        "Union member '{0}' should match the configured casing convention",
        "Union types and their cases follow the configured casing (default PascalCase); set 'stylesharp_union_member_naming' in .editorconfig to override.");

    /// <summary>SST1316 — tuple element names should use the configured casing (default PascalCase).</summary>
    public static readonly DiagnosticDescriptor TupleElement = Create(
        "SST1316",
        "Tuple element names should match the configured casing",
        "Tuple element '{0}' should match the configured casing convention",
        "Tuple element names follow the configured casing (default PascalCase); set 'stylesharp_tuple_element_naming' in .editorconfig to override.");

    /// <summary>Every fixable naming id, for the shared rename code fix.</summary>
    public static readonly ImmutableArray<string> AllFixableIds = ImmutableArrays.Of(
        "SST1300",
        "SST1302",
        "SST1303",
        "SST1304",
        "SST1307",
        "SST1309",
        "SST1311",
        "SST1312",
        "SST1313",
        "SST1314",
        "SST1315");

    /// <summary>Creates a Warning-severity Naming descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format (the offending name is argument 0).</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Naming",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
