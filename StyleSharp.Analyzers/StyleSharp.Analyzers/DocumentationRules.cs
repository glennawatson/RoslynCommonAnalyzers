// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the documentation (SST16xx) diagnostic descriptors.
/// </summary>
internal static class DocumentationRules
{
    /// <summary>SST1600 — externally visible members should be documented.</summary>
    public static readonly DiagnosticDescriptor ElementsMustBeDocumented = Create(
        "SST1600",
        "Elements should be documented",
        "'{0}' is externally visible and should have a documentation comment",
        "Public and protected members carry XML documentation so the generated docs and IntelliSense are complete.");

    /// <summary>SST1602 — enumeration members should be documented.</summary>
    public static readonly DiagnosticDescriptor EnumItemsMustBeDocumented = Create(
        "SST1602",
        "Enumeration items should be documented",
        "Enum member '{0}' should have a documentation comment",
        "Members of an externally visible enum carry XML documentation.");

    /// <summary>SST1604 — element documentation should contain a summary.</summary>
    public static readonly DiagnosticDescriptor MustHaveSummary = Create(
        "SST1604",
        "Element documentation should have a summary",
        "Documentation for '{0}' should contain a <summary>",
        "A documentation comment should include a <summary> (unless it uses <inheritdoc>).");

    /// <summary>SST1606 — the summary should have text.</summary>
    public static readonly DiagnosticDescriptor SummaryMustHaveText = Create(
        "SST1606",
        "Summary documentation should have text",
        "The <summary> for '{0}' should not be empty",
        "A <summary> element should contain descriptive text.");

    /// <summary>SST1611 — parameters should be documented.</summary>
    public static readonly DiagnosticDescriptor ParametersMustBeDocumented = Create(
        "SST1611",
        "Parameters should be documented",
        "Parameter '{0}' should have a <param> documentation element",
        "Each parameter of a documented member has a matching <param> element.");

    /// <summary>SST1615 — the return value should be documented.</summary>
    public static readonly DiagnosticDescriptor ReturnValueMustBeDocumented = Create(
        "SST1615",
        "Return value should be documented",
        "Documentation for '{0}' should contain a <returns>",
        "A documented member with a non-void return type has a <returns> element.");

    /// <summary>SST1617 — a void return value should not be documented.</summary>
    public static readonly DiagnosticDescriptor VoidMustNotHaveReturn = Create(
        "SST1617",
        "Void return value should not be documented",
        "Documentation for '{0}' should not contain a <returns> (it returns void)",
        "A member that returns void does not have a <returns> element.");

    /// <summary>SST1618 — generic type parameters should be documented.</summary>
    public static readonly DiagnosticDescriptor TypeParametersMustBeDocumented = Create(
        "SST1618",
        "Generic type parameters should be documented",
        "Type parameter '{0}' should have a <typeparam> documentation element",
        "Each generic type parameter of a documented member has a matching <typeparam> element.");

    /// <summary>SST1623 — property summaries should describe their accessors ("Gets", "Sets", "Gets or sets").</summary>
    public static readonly DiagnosticDescriptor PropertySummaryAccessors = Create(
        "SST1623",
        "Property summary should describe its accessors",
        "Property summary should start with '{0}'",
        "A property's summary begins with 'Gets', 'Sets', or 'Gets or sets' to match its accessors.");

    /// <summary>SST1642 — constructor summaries should begin with the standard text.</summary>
    public static readonly DiagnosticDescriptor ConstructorStandardText = Create(
        "SST1642",
        "Constructor summary should begin with the standard text",
        "Constructor summary should begin with 'Initializes a new instance of the <see cref=\"...\"/> class.'",
        "A constructor's summary begins with the standard 'Initializes a new instance of the <see cref=\"Type\"/> class.' text.");

    /// <summary>SST1629 — documentation text should end with a period.</summary>
    public static readonly DiagnosticDescriptor TextMustEndWithPeriod = Create(
        "SST1629",
        "Documentation text should end with a period",
        "This documentation text should end with a period",
        "Documentation sentences end with a period (or other terminal punctuation).");

    /// <summary>
    /// SST1653 — a <c>&lt;summary&gt;</c> whose text fits within the configured
    /// length should be written on a single line. StyleSharp-original (no StyleCop
    /// equivalent); the length is set with <c>stylesharp.summary_single_line_max_length</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor SingleLineSummary = Create(
        "SST1653",
        "Short documentation summaries should be on a single line",
        "This summary is short enough to fit on a single line",
        "A '<summary>' whose combined text is shorter than the configured limit (default 100 characters) should be written on one line: '/// <summary>Text</summary>'.");

    /// <summary>Creates a Warning-severity Documentation descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Documentation",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
