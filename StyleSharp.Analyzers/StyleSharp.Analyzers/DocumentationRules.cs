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

    /// <summary>SST1608 — documentation should not use the default placeholder summary.</summary>
    public static readonly DiagnosticDescriptor NoDefaultSummary = Create(
        "SST1608",
        "Documentation should not use placeholder text",
        "The <summary> for '{0}' still contains placeholder text",
        "A summary should describe the element rather than keep the generated placeholder text.");

    /// <summary>SST1611 — parameters should be documented.</summary>
    public static readonly DiagnosticDescriptor ParametersMustBeDocumented = Create(
        "SST1611",
        "Parameters should be documented",
        "Parameter '{0}' should have a <param> documentation element",
        "Each parameter of a documented member has a matching <param> element.");

    /// <summary>SST1612 — parameter documentation should match the parameters.</summary>
    public static readonly DiagnosticDescriptor ParameterDocumentationMustMatch = Create(
        "SST1612",
        "Parameter documentation should match the parameters",
        "<param name=\"{0}\"> does not match any parameter",
        "Each <param> element refers to a real parameter of the member.");

    /// <summary>SST1613 — parameter documentation should declare a name.</summary>
    public static readonly DiagnosticDescriptor ParameterDocumentationMustDeclareName = Create(
        "SST1613",
        "Parameter documentation should declare the parameter name",
        "A <param> element should have a name attribute",
        "Each <param> element has a name attribute identifying the parameter it documents.");

    /// <summary>SST1614 — parameter documentation should have text.</summary>
    public static readonly DiagnosticDescriptor ParameterDocumentationMustHaveText = Create(
        "SST1614",
        "Parameter documentation should have text",
        "The <param> element for '{0}' should not be empty",
        "A <param> element contains descriptive text.");

    /// <summary>SST1616 — return value documentation should have text.</summary>
    public static readonly DiagnosticDescriptor ReturnDocumentationMustHaveText = Create(
        "SST1616",
        "Return value documentation should have text",
        "The <returns> element should not be empty",
        "A <returns> element contains descriptive text.");

    /// <summary>SST1620 — type parameter documentation should match the type parameters.</summary>
    public static readonly DiagnosticDescriptor TypeParameterDocumentationMustMatch = Create(
        "SST1620",
        "Type parameter documentation should match the type parameters",
        "<typeparam name=\"{0}\"> does not match any type parameter",
        "Each <typeparam> element refers to a real type parameter of the member.");

    /// <summary>SST1621 — type parameter documentation should declare a name.</summary>
    public static readonly DiagnosticDescriptor TypeParameterDocumentationMustDeclareName = Create(
        "SST1621",
        "Type parameter documentation should declare the type parameter name",
        "A <typeparam> element should have a name attribute",
        "Each <typeparam> element has a name attribute identifying the type parameter it documents.");

    /// <summary>SST1622 — type parameter documentation should have text.</summary>
    public static readonly DiagnosticDescriptor TypeParameterDocumentationMustHaveText = Create(
        "SST1622",
        "Type parameter documentation should have text",
        "The <typeparam> element for '{0}' should not be empty",
        "A <typeparam> element contains descriptive text.");

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

    /// <summary>SST1624 — a property summary mentions a restricted setter (opt-in).</summary>
    public static readonly DiagnosticDescriptor PropertySummaryOmitsRestrictedSetter = CreateOptIn(
        "SST1624",
        "Property summary should omit a restricted set accessor",
        "Property summary should start with 'Gets' because the set accessor is more restricted",
        "A property's public summary omits a setter or initializer whose accessibility is more restrictive than the property.");

    /// <summary>SST1627 — a documentation section contains no text (opt-in).</summary>
    public static readonly DiagnosticDescriptor DocumentationTextNotEmpty = CreateOptIn(
        "SST1627",
        "Documentation text should not be empty",
        "This documentation section should contain text",
        "Non-summary documentation sections such as remarks and paragraphs contain descriptive text.");

    /// <summary>SST1644 — a documentation comment contains an interior blank line (opt-in).</summary>
    public static readonly DiagnosticDescriptor DocumentationHeaderNoBlankLines = CreateOptIn(
        "SST1644",
        "Documentation headers should not contain blank lines",
        "Remove this blank documentation line",
        "Documentation prose remains contiguous rather than containing empty '///' lines. Off by default because the upstream StyleCop rule is deprecated.");

    /// <summary>SST1642 — constructor summaries should begin with the standard text.</summary>
    public static readonly DiagnosticDescriptor ConstructorStandardText = Create(
        "SST1642",
        "Constructor summary should begin with the standard text",
        "Constructor summary should begin with 'Initializes a new instance of the <see cref=\"...\"/> class.'",
        "A constructor's summary begins with the standard 'Initializes a new instance of the <see cref=\"Type\"/> class.' text.");

    /// <summary>SST1649 — the file name should match the first type name.</summary>
    public static readonly DiagnosticDescriptor FileNameMatchesType = Create(
        "SST1649",
        "File name should match the first type name",
        "File name should match the first type '{0}'",
        "A file is named after the first type it declares (ignoring generic arity and any '.suffix' before the extension).");

    /// <summary>SST1633 — files should begin with the configured header.</summary>
    public static readonly DiagnosticDescriptor FileHeader = Create(
        "SST1633",
        "File should have a header",
        "File should begin with the header configured by 'file_header_template'",
        "When 'file_header_template' is set in .editorconfig, every file begins with the rendered header comment.");

    /// <summary>SST1643 — destructor summaries should begin with the standard text.</summary>
    public static readonly DiagnosticDescriptor DestructorStandardText = Create(
        "SST1643",
        "Destructor summary should begin with the standard text",
        "Destructor summary should begin with 'Finalizes an instance of the <see cref=\"...\"/> class.'",
        "A destructor's summary begins with the standard 'Finalizes an instance of the <see cref=\"Type\"/> class.' text.");

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

    /// <summary>SST1626 — a documentation-style comment is used where it does not document an element.</summary>
    public static readonly DiagnosticDescriptor NoDocumentationStyleComment = Create(
        "SST1626",
        "Single-line comments should not use documentation style slashes",
        "Use '//' for this comment; '///' is reserved for element documentation",
        "A '///' comment is only used to document a type or member; elsewhere a '//' comment is used.");

    /// <summary>SST1651 — placeholder documentation elements should be removed.</summary>
    public static readonly DiagnosticDescriptor NoPlaceholderElements = Create(
        "SST1651",
        "Documentation should not contain placeholder text",
        "Replace the '<placeholder>' element with real documentation",
        "Generated '<placeholder>' documentation elements are replaced with real content.");

    /// <summary>SST1601 — partial elements should be documented.</summary>
    public static readonly DiagnosticDescriptor PartialMustBeDocumented = Create(
        "SST1601",
        "Partial elements should be documented",
        "Partial '{0}' should have a documentation comment",
        "Partial types and methods carry XML documentation.");

    /// <summary>SST1605 — partial element documentation should have a summary (opt-in).</summary>
    public static readonly DiagnosticDescriptor PartialMustHaveSummary = CreateOptIn(
        "SST1605",
        "Partial element documentation should have a summary",
        "Documentation for partial '{0}' should contain a <summary>",
        "A documented partial element includes a <summary>. Off by default — documenting one part of a partial is often enough.");

    /// <summary>SST1607 — partial element summary should have text (opt-in).</summary>
    public static readonly DiagnosticDescriptor PartialSummaryMustHaveText = CreateOptIn(
        "SST1607",
        "Partial element summary should have text",
        "The <summary> for partial '{0}' should not be empty",
        "A documented partial element's <summary> contains text. Off by default.");

    /// <summary>SST1609 — property documentation should have a value element (opt-in).</summary>
    public static readonly DiagnosticDescriptor PropertyMustHaveValue = CreateOptIn(
        "SST1609",
        "Property documentation should have a value",
        "Documentation for property '{0}' should contain a <value>",
        "A documented property includes a <value> element. Off by default, matching StyleCop.");

    /// <summary>SST1610 — property value element should have text (opt-in).</summary>
    public static readonly DiagnosticDescriptor PropertyValueMustHaveText = CreateOptIn(
        "SST1610",
        "Property value documentation should have text",
        "The <value> for property '{0}' should not be empty",
        "A property's <value> element contains text. Off by default, matching StyleCop.");

    /// <summary>SST1619 — generic type parameters of a partial type should be documented (opt-in).</summary>
    public static readonly DiagnosticDescriptor PartialTypeParametersDocumented = CreateOptIn(
        "SST1619",
        "Partial generic type parameters should be documented",
        "Type parameter '{0}' should have a matching <typeparam>",
        "Each generic type parameter of a documented partial type has a <typeparam> element. Off by default.");

    /// <summary>SST1625 — element documentation should not be copy-pasted.</summary>
    public static readonly DiagnosticDescriptor NoDuplicateDocumentation = Create(
        "SST1625",
        "Element documentation should not be copy-pasted",
        "This documentation text duplicates another element's text",
        "Each documentation element describes its own element rather than repeating another's text verbatim.");

    /// <summary>SST1628 — documentation text should begin with a capital letter (opt-in).</summary>
    public static readonly DiagnosticDescriptor TextBeginsWithCapital = CreateOptIn(
        "SST1628",
        "Documentation text should begin with a capital letter",
        "The documentation summary should begin with a capital letter",
        "Documentation summary text begins with a capital letter. Off by default, matching StyleCop.");

    /// <summary>SST1630 — documentation text should contain whitespace between words (opt-in).</summary>
    public static readonly DiagnosticDescriptor TextContainsWhitespace = CreateOptIn(
        "SST1630",
        "Documentation text should contain whitespace",
        "The documentation summary should contain more than one word",
        "Documentation summary text contains whitespace (more than a single word). Off by default, matching StyleCop.");

    /// <summary>SST1631 — documentation text should be mostly letters (opt-in).</summary>
    public static readonly DiagnosticDescriptor TextCharacterPercentage = CreateOptIn(
        "SST1631",
        "Documentation text should be mostly letters",
        "The documentation summary should be made up mostly of letters, not symbols",
        "At least half of a documentation summary's characters are letters or whitespace. Off by default, matching StyleCop.");

    /// <summary>SST1632 — documentation text should meet a minimum length (opt-in).</summary>
    public static readonly DiagnosticDescriptor TextMinimumLength = CreateOptIn(
        "SST1632",
        "Documentation text should meet a minimum length",
        "The documentation summary is too short to be meaningful",
        "Documentation summary text is at least a few characters long. Off by default, matching StyleCop.");

    /// <summary>SST1648 — inheritdoc should only be used on inheriting elements.</summary>
    public static readonly DiagnosticDescriptor InheritDocValid = Create(
        "SST1648",
        "inheritdoc should be used with inheriting elements",
        "'{0}' uses <inheritdoc> but does not inherit from or implement a documented base",
        "An <inheritdoc> element is only used where the element inherits or implements a base member to inherit documentation from.");

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

    /// <summary>Creates a Documentation descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Documentation",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
