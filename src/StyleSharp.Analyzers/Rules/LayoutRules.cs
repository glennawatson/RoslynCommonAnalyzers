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

    /// <summary>SST1521 — a line is longer than the configured maximum.</summary>
    public static readonly DiagnosticDescriptor LineTooLong = Create(
        "SST1521",
        "Lines should not be too long",
        "This line is {0} characters, over the maximum of {1}",
        LineTooLongDescription);

    /// <summary>SST1522 — a file declares more lines than the configured maximum.</summary>
    public static readonly DiagnosticDescriptor FileTooLong = Create(
        "SST1522",
        "Files should not be too long",
        "This file is {0} lines, over the maximum of {1}",
        FileTooLongDescription);

    /// <summary>SST1523 — a member's body is longer than the configured maximum.</summary>
    public static readonly DiagnosticDescriptor MethodTooLong = Create(
        "SST1523",
        "Members should not be too long",
        "'{0}' is {1} lines, over the maximum of {2}",
        MethodTooLongDescription);

    /// <summary>SST1524 — a switch section is longer than the configured maximum.</summary>
    public static readonly DiagnosticDescriptor SwitchSectionTooLong = Create(
        "SST1524",
        "Switch sections should not be too long",
        "This case is {0} lines, over the maximum of {1}",
        SwitchSectionTooLongDescription);

    /// <summary>SST1525 — a switch section holds several statements without wrapping them in braces.</summary>
    public static readonly DiagnosticDescriptor SwitchSectionBraces = Create(
        "SST1525",
        "Switch sections with multiple statements should use braces",
        "Wrap the body of this switch section in braces",
        SwitchSectionBracesDescription);

    /// <summary>SST1526 — a wrapped binary operator sits on the wrong side of its line break.</summary>
    public static readonly DiagnosticDescriptor BinaryOperatorNewLine = CreateOptIn(
        "SST1526",
        "Wrapped binary operators should sit on the configured side of the line break",
        "Place the binary operator '{0}' at the {1} of the wrapped line",
        BinaryOperatorNewLineDescription);

    /// <summary>SST1527 — a wrapped expression-body arrow sits on the wrong side of its line break.</summary>
    public static readonly DiagnosticDescriptor ArrowTokenNewLine = CreateOptIn(
        "SST1527",
        "A wrapped expression-body arrow should sit on the configured side of the line break",
        "Place '=>' at the {0} of the line break",
        ArrowTokenNewLineDescription);

    /// <summary>SST1528 — a wrapped initializer equals sign sits on the wrong side of its line break.</summary>
    public static readonly DiagnosticDescriptor EqualsTokenNewLine = CreateOptIn(
        "SST1528",
        "A wrapped initializer '=' should sit on the configured side of the line break",
        "Place '=' at the {0} of the line break",
        EqualsTokenNewLineDescription);

    /// <summary>SST1529 — a wrapped call-chain '.'/'?.' sits on the wrong side of its line break.</summary>
    public static readonly DiagnosticDescriptor NullConditionalNewLine = CreateOptIn(
        "SST1529",
        "Wrapped call-chain operators should sit on the configured side of the line break",
        "Place '{0}' at the {1} of the wrapped line",
        NullConditionalNewLineDescription);

    /// <summary>SST1530 — a type declaration's base list starts on its own line.</summary>
    public static readonly DiagnosticDescriptor BaseListOnDeclarationLine = CreateOptIn(
        "SST1530",
        "A base list should not start on its own line",
        "Place the base list on the same line as the type declaration",
        BaseListOnDeclarationLineDescription);

    /// <summary>SST1531 — a short initializer is split across several lines.</summary>
    public static readonly DiagnosticDescriptor InitializerOnSingleLine = CreateOptIn(
        "SST1531",
        "A short initializer should be written on a single line",
        "Collapse the initializer onto a single line",
        InitializerOnSingleLineDescription);

    /// <summary>SST1532 — the file mixes line-ending styles or uses the non-configured one.</summary>
    public static readonly DiagnosticDescriptor ConsistentLineEndings = CreateOptIn(
        "SST1532",
        "Files should use consistent line endings",
        "Use {0} line endings throughout the file",
        ConsistentLineEndingsDescription);

    /// <summary>SST1533 — a source file declares no type, only usings or comments.</summary>
    public static readonly DiagnosticDescriptor FileWithoutCode = CreateOptIn(
        "SST1533",
        "A source file should declare at least one type",
        "This file declares no type, only usings or comments",
        FileWithoutCodeDescription);

    /// <summary>The SwitchSectionBraces rule description.</summary>
    private const string SwitchSectionBracesDescription =
        "A switch section that carries more than one statement wraps them in a brace-delimited block. The braces give the "
        + "section its own scope and a single closing point, so the reader sees where the case starts and ends without "
        + "counting the labels between it and the next one. This is the switch-section form of the always-braces house "
        + "style the control-flow rules apply to an if or a loop body.";

    /// <summary>The BinaryOperatorNewLine rule description.</summary>
    private const string BinaryOperatorNewLineDescription =
        "When a binary expression is broken across lines, the operator has two homes: the end of the upper line or the "
        + "start of the lower one. Either is fine; a file that mixes them is not, because the reader loses the thread of "
        + "where one operand ends and the next begins. The whole wrapped chain is held to one placement. The default leads "
        + "the continuation line with the operator; set 'stylesharp.binary_operator_new_line' to 'after' to trail the "
        + "upper line instead.";

    /// <summary>The ArrowTokenNewLine rule description.</summary>
    private const string ArrowTokenNewLineDescription =
        "An expression-bodied member whose body wraps onto its own line can keep the arrow trailing the signature or lead "
        + "the body line with it. The default trails the signature, so the eye reaches the arrow before dropping to the "
        + "expression; set 'stylesharp.arrow_token_new_line' to 'before' to lead the body line with the arrow instead.";

    /// <summary>The EqualsTokenNewLine rule description.</summary>
    private const string EqualsTokenNewLineDescription =
        "A field or local whose initializer wraps onto its own line can keep the equals sign trailing the name or lead the "
        + "value line with it. The default trails the name; set 'stylesharp.equals_token_new_line' to 'before' to lead the "
        + "value line with the equals sign instead.";

    /// <summary>The NullConditionalNewLine rule description.</summary>
    private const string NullConditionalNewLineDescription =
        "A fluent call chain that wraps reads as a column of steps, one member access per line, each '.' or '?.' aligned "
        + "under the last. A step that shares a line with its neighbour, or an operator that trails the upper line while "
        + "the rest lead the lower one, breaks the column. The default leads each continuation line with the operator; set "
        + "'stylesharp.null_conditional_new_line' to 'after' to trail the upper line instead.";

    /// <summary>The BaseListOnDeclarationLine rule description.</summary>
    private const string BaseListOnDeclarationLineDescription =
        "A base list pushed onto its own line separates a type from the very thing that says what it is, and leaves a lone "
        + "colon hanging under the name. When the declaration and its bases fit within the line limit together, they belong "
        + "on one line. A base list that only fits by wrapping is left alone.";

    /// <summary>The InitializerOnSingleLine rule description.</summary>
    private const string InitializerOnSingleLineDescription =
        "An object or collection initializer short enough to fit on one line reads better on one line: the multi-line form "
        + "spends four lines to say what one line says, and the extra lines carry no information. Only initializers that "
        + "stay within the line limit once collapsed are reported, so a genuinely long initializer keeps its lines. This is "
        + "the initializer counterpart to the rules that instead expand a collapsed statement block.";

    /// <summary>The ConsistentLineEndings rule description.</summary>
    private const string ConsistentLineEndingsDescription =
        "Line endings that vary within a file, or differ from the project's chosen style, turn every diff into a wall of "
        + "changed lines and make a merge fight over whitespace nobody typed. One style is enforced per file. The default "
        + "is LF; set 'stylesharp.line_ending' to 'crlf' to require carriage-return/line-feed instead.";

    /// <summary>The FileWithoutCode rule description.</summary>
    private const string FileWithoutCodeDescription =
        "A .cs file that carries usings or comments but declares no namespace, type, or top-level statement is a file that "
        + "has lost its contents to a move or a delete and left its header behind. It compiles, so nothing complains, and "
        + "it lingers as a dead entry in every file list. A genuinely empty file is left alone.";

    /// <summary>The LineTooLong rule description.</summary>
    private const string LineTooLongDescription =
        "A line that runs past the edge of the window is read by scrolling, and a line read by scrolling is read badly — the end of it "
        + "disappears while the reader looks at the start. It also defeats side-by-side diffs, which is where most code is actually read. "
        + "Configure the maximum with 'stylesharp.SST1521.max_line_length'; it defaults to 120.";

    /// <summary>The FileTooLong rule description.</summary>
    private const string FileTooLongDescription =
        "A file nobody can hold in their head is a file that grows by accretion, because finding the right place to add something is harder "
        + "than appending. The limit is a prompt to split, not a law of nature. Blank lines and comments are not counted — only lines that "
        + "carry code. Configure the maximum with 'stylesharp.SST1522.max_file_lines'; it defaults to 500.";

    /// <summary>The MethodTooLong rule description.</summary>
    private const string MethodTooLongDescription =
        "Length is a cruder measure than complexity — a long method of straight-line assignments is easier to read than a short one with "
        + "four nested loops, and the complexity rules already catch the second. What length catches is the method that is long because it "
        + "does several things, and would read better as several methods. Blank lines and comments are not counted. Configure the maximum "
        + "with 'stylesharp.SST1523.max_member_lines'; it defaults to 60.";

    /// <summary>The SwitchSectionTooLong rule description.</summary>
    private const string SwitchSectionTooLongDescription =
        "A switch reads as a table: one row per case, each saying what happens. A case that runs to dozens of lines stops being a row and "
        + "becomes a method that has not been extracted yet, and the shape of the switch — the thing the reader came for — is lost between "
        + "them. Configure the maximum with 'stylesharp.SST1524.max_switch_section_lines'; it defaults to 20.";

    /// <summary>Creates a Warning-severity Layout descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Layout", description);

    /// <summary>Creates a disabled-by-default (opt-in) Layout descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Layout", description);
}
