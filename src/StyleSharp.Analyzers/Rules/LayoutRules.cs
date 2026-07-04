// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the layout (SST15xx) diagnostic descriptors. The
/// repository does not flag brace omission here — that concern is deferred elsewhere.
/// </summary>
internal static class LayoutRules
{
    /// <summary>SST1500 — a brace in a multi-line construct shares its line with other code.</summary>
    public static readonly DiagnosticDescriptor BracesOnOwnLine = Create(
        "SST1500",
        "Braces for multi-line statements should not share a line",
        "Place this brace on its own line",
        "In a multi-line construct each brace sits on its own line so the block structure is unambiguous.");

    /// <summary>SST1501 — a statement block is collapsed onto a single line.</summary>
    public static readonly DiagnosticDescriptor StatementOnOwnLine = Create(
        "SST1501",
        "Statement should not be on a single line",
        "Place the statement on its own line, not sharing a line with the braces",
        "A statement is written on its own line rather than collapsed inside single-line braces.");

    /// <summary>SST1502 — an element body is collapsed onto a single line.</summary>
    public static readonly DiagnosticDescriptor ElementOnOwnLine = Create(
        "SST1502",
        "Element should not be on a single line",
        "Place the body of '{0}' on its own lines",
        "A non-empty element body spans multiple lines rather than being collapsed onto one.");

    /// <summary>SST1503 — a control-flow statement omits the braces around its child statement.</summary>
    public static readonly DiagnosticDescriptor BracesRequired = Create(
        "SST1503",
        "Braces should not be omitted",
        "Add braces around the child statement",
        "The child statement of a control-flow statement is always wrapped in braces.");

    /// <summary>SST1504 — the accessors of a property/event mix single-line and multi-line forms.</summary>
    public static readonly DiagnosticDescriptor AccessorLineConsistency = Create(
        "SST1504",
        "All accessors should be single-line or multi-line",
        "Make all accessors consistently single-line or multi-line",
        "Either every accessor is written on a single line, or every accessor spans multiple lines.");

    /// <summary>SST1505 — an opening brace is followed by a blank line.</summary>
    public static readonly DiagnosticDescriptor OpenBraceNotFollowedByBlankLine = Create(
        "SST1505",
        "Opening brace should not be followed by a blank line",
        "Remove the blank line after the opening brace",
        "An opening brace is immediately followed by code, not a blank line.");

    /// <summary>SST1506 — an element documentation header is followed by a blank line.</summary>
    public static readonly DiagnosticDescriptor DocHeaderNotFollowedByBlankLine = Create(
        "SST1506",
        "Element documentation header should not be followed by a blank line",
        "Remove the blank line between the documentation header and the element",
        "A documentation header sits directly above the element it documents.");

    /// <summary>SST1507 — two or more blank lines appear in a row.</summary>
    public static readonly DiagnosticDescriptor MultipleBlankLines = Create(
        "SST1507",
        "Code should not contain multiple blank lines in a row",
        "Remove the extra blank line(s)",
        "Consecutive blank lines are collapsed to a single blank line.");

    /// <summary>SST1508 — a closing brace is preceded by a blank line.</summary>
    public static readonly DiagnosticDescriptor CloseBraceNotPrecededByBlankLine = Create(
        "SST1508",
        "Closing brace should not be preceded by a blank line",
        "Remove the blank line before the closing brace",
        "A closing brace follows code directly, not a blank line.");

    /// <summary>SST1509 — an opening brace is preceded by a blank line.</summary>
    public static readonly DiagnosticDescriptor OpenBraceNotPrecededByBlankLine = Create(
        "SST1509",
        "Opening brace should not be preceded by a blank line",
        "Remove the blank line before the opening brace",
        "An opening brace follows its declaration directly, not a blank line.");

    /// <summary>SST1510 — a chained block (else/catch/finally) is preceded by a blank line.</summary>
    public static readonly DiagnosticDescriptor ChainedBlockNotPrecededByBlankLine = Create(
        "SST1510",
        "Chained statement block should not be preceded by a blank line",
        "Remove the blank line before '{0}'",
        "A chained 'else', 'catch', or 'finally' follows the preceding block directly.");

    /// <summary>SST1511 — the 'while' footer of a do/while loop is preceded by a blank line.</summary>
    public static readonly DiagnosticDescriptor WhileFooterNotPrecededByBlankLine = Create(
        "SST1511",
        "Do/while footer should not be preceded by a blank line",
        "Remove the blank line before the 'while' footer",
        "The 'while' footer of a do/while loop follows the loop body directly.");

    /// <summary>SST1512 — a single-line comment is followed by a blank line.</summary>
    public static readonly DiagnosticDescriptor SingleLineCommentNotFollowedByBlankLine = Create(
        "SST1512",
        "Single-line comment should not be followed by a blank line",
        "Remove the blank line after the comment",
        "A single-line comment sits directly above the code it describes.");

    /// <summary>SST1513 — a closing brace is not followed by a blank line.</summary>
    public static readonly DiagnosticDescriptor CloseBraceFollowedByBlankLine = Create(
        "SST1513",
        "Closing brace should be followed by a blank line",
        "Add a blank line after the closing brace",
        "A closing brace that ends a statement or member is followed by a blank line.");

    /// <summary>SST1514 — an element documentation header is not preceded by a blank line.</summary>
    public static readonly DiagnosticDescriptor DocHeaderPrecededByBlankLine = Create(
        "SST1514",
        "Element documentation header should be preceded by a blank line",
        "Add a blank line before the documentation header",
        "A documentation header is separated from the preceding element by a blank line.");

    /// <summary>SST1515 — a single-line comment is not preceded by a blank line.</summary>
    public static readonly DiagnosticDescriptor SingleLineCommentPrecededByBlankLine = Create(
        "SST1515",
        "Single-line comment should be preceded by a blank line",
        "Add a blank line before the comment",
        "A single-line comment is separated from the preceding code by a blank line.");

    /// <summary>SST1516 — adjacent members or namespace elements are not separated by a blank line.</summary>
    public static readonly DiagnosticDescriptor ElementsSeparatedByBlankLine = Create(
        "SST1516",
        "Elements should be separated by a blank line",
        "Add a blank line to separate the elements",
        "Adjacent members, usings groups, and namespace elements are separated by a blank line.");

    /// <summary>SST1517 — the file begins with one or more blank lines.</summary>
    public static readonly DiagnosticDescriptor NoBlankLinesAtStartOfFile = Create(
        "SST1517",
        "Code should not contain blank lines at the start of the file",
        "Remove the blank line(s) at the start of the file",
        "A file starts with code or a header comment, not a blank line.");

    /// <summary>SST1518 — the file does not end with exactly one newline.</summary>
    public static readonly DiagnosticDescriptor LineEndingsAtEndOfFile = Create(
        "SST1518",
        "File should end with a single newline",
        "End the file with a single newline",
        "A file ends with exactly one trailing newline and no blank lines.");

    /// <summary>SST1519 — a multi-line child statement of a control-flow keyword omits its braces.</summary>
    public static readonly DiagnosticDescriptor BracesForMultiLineChild = Create(
        "SST1519",
        "Braces should not be omitted from a multi-line child statement",
        "Add braces around the multi-line child statement",
        "A child statement that spans multiple lines is wrapped in braces.");

    /// <summary>SST1520 — the clauses of an if/else chain use braces inconsistently.</summary>
    public static readonly DiagnosticDescriptor BracesUsedConsistently = Create(
        "SST1520",
        "Braces should be used consistently",
        "Add braces to all clauses of the statement",
        "Every clause of an if/else chain uses braces, or none does.");

    /// <summary>Creates a Warning-severity Layout descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Layout", description);
}
