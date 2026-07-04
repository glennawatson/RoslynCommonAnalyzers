// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the spacing (SST10xx) diagnostic descriptors.
/// </summary>
internal static class SpacingRules
{
    /// <summary>SST1005 — a single-line comment does not begin with a single space.</summary>
    public static readonly DiagnosticDescriptor CommentBeginsWithSpace = Create(
        "SST1005",
        "Single-line comments should begin with a single space",
        "Add a space after the '//'",
        "A single-line comment begins with '// ' so the text is easy to read.");

    /// <summary>SST1004 — a documentation comment line does not begin with a single space after the '///'.</summary>
    public static readonly DiagnosticDescriptor DocumentationBeginsWithSpace = Create(
        "SST1004",
        "Documentation lines should begin with a single space",
        "Add a space after the '///'",
        "A documentation comment line begins with '/// ' so the documentation text is easy to read.");

    /// <summary>SST1006 — a preprocessor keyword is preceded by a space after the '#'.</summary>
    public static readonly DiagnosticDescriptor PreprocessorKeywordSpacing = Create(
        "SST1006",
        "Preprocessor keywords should not be preceded by a space",
        "Remove the space between the '#' and the preprocessor keyword",
        "A preprocessor keyword immediately follows the '#' with no space (for example '#if', not '# if').");

    /// <summary>SST1025 — two or more whitespace characters appear in a row within a line.</summary>
    public static readonly DiagnosticDescriptor MultipleWhitespace = Create(
        "SST1025",
        "Code should not contain multiple whitespace characters in a row",
        "Replace the whitespace with a single space",
        "A single space separates code on a line; runs of whitespace (for example for alignment) are collapsed.");

    /// <summary>SST1027 — a tab character is used where the project standardises on spaces.</summary>
    public static readonly DiagnosticDescriptor UseSpacesNotTabs = Create(
        "SST1027",
        "Use spaces rather than tabs",
        "Replace the tab(s) with spaces",
        "Whitespace is made of spaces rather than tab characters.");

    /// <summary>SST1028 — a line ends with trailing whitespace.</summary>
    public static readonly DiagnosticDescriptor NoTrailingWhitespace = Create(
        "SST1028",
        "Code should not contain trailing whitespace",
        "Remove the trailing whitespace",
        "Lines end at their last non-whitespace character, with no trailing spaces or tabs.");

    /// <summary>SST1001 — a comma is spaced incorrectly.</summary>
    public static readonly DiagnosticDescriptor CommaSpacing = Create(
        "SST1001",
        "Commas should be spaced correctly",
        "A comma should be followed by a single space and not preceded by one",
        "A comma is followed by a space (or newline) and is not preceded by whitespace.");

    /// <summary>SST1002 — a semicolon is spaced incorrectly.</summary>
    public static readonly DiagnosticDescriptor SemicolonSpacing = Create(
        "SST1002",
        "Semicolons should be spaced correctly",
        "A semicolon should be followed by a single space and not preceded by one",
        "A semicolon is followed by a space (or newline) and is not preceded by whitespace.");

    /// <summary>SST1014 — an opening generic bracket is preceded or followed by a space.</summary>
    public static readonly DiagnosticDescriptor OpeningGenericBracket = Create(
        "SST1014",
        "Opening generic brackets should be spaced correctly",
        "An opening generic bracket should not be preceded or followed by a space",
        "The '<' of a generic argument or parameter list has no adjacent whitespace.");

    /// <summary>SST1015 — a closing generic bracket is preceded by a space.</summary>
    public static readonly DiagnosticDescriptor ClosingGenericBracket = Create(
        "SST1015",
        "Closing generic brackets should be spaced correctly",
        "A closing generic bracket should not be preceded by a space",
        "The '>' of a generic argument or parameter list is not preceded by whitespace.");

    /// <summary>SST1016 — an opening attribute bracket is followed by a space.</summary>
    public static readonly DiagnosticDescriptor OpeningAttributeBracket = Create(
        "SST1016",
        "Opening attribute brackets should be spaced correctly",
        "An opening attribute bracket should not be followed by a space",
        "The '[' of an attribute list is not followed by whitespace.");

    /// <summary>SST1017 — a closing attribute bracket is preceded by a space.</summary>
    public static readonly DiagnosticDescriptor ClosingAttributeBracket = Create(
        "SST1017",
        "Closing attribute brackets should be spaced correctly",
        "A closing attribute bracket should not be preceded by a space",
        "The ']' of an attribute list is not preceded by whitespace.");

    /// <summary>SST1018 — a nullable type symbol is preceded by a space.</summary>
    public static readonly DiagnosticDescriptor NullableSpacing = Create(
        "SST1018",
        "Nullable type symbols should not be preceded by a space",
        "Remove the space before the nullable '?'",
        "The '?' of a nullable type immediately follows the type name with no whitespace.");

    /// <summary>SST1019 — a member access symbol is surrounded by spaces.</summary>
    public static readonly DiagnosticDescriptor MemberAccessSpacing = Create(
        "SST1019",
        "Member access symbols should be spaced correctly",
        "A member access '.' should not be surrounded by spaces",
        "A member access '.' has no adjacent whitespace (a leading-dot on its own line is allowed).");

    /// <summary>SST1026 — a space follows 'new' or 'stackalloc' in an implicit array creation.</summary>
    public static readonly DiagnosticDescriptor ImplicitArraySpacing = Create(
        "SST1026",
        "Implicit array creation should not contain a space before the bracket",
        "Remove the space before the '['",
        "An implicit array creation ('new[]' or 'stackalloc[]') has no space before the bracket.");

    /// <summary>SST1000 — a control-flow keyword is not followed by a space.</summary>
    public static readonly DiagnosticDescriptor KeywordSpacing = Create(
        "SST1000",
        "Keywords should be spaced correctly",
        "A keyword should be followed by a space",
        "A control-flow keyword (if, while, for, …) is followed by a space before its opening parenthesis.");

    /// <summary>SST1003 — a binary operator is not surrounded by spaces.</summary>
    public static readonly DiagnosticDescriptor OperatorSpacing = Create(
        "SST1003",
        "Symbols should be spaced correctly",
        "A binary operator should be surrounded by single spaces",
        "A binary or assignment operator has a single space on each side.");

    /// <summary>SST1008 — an opening parenthesis is followed by a space.</summary>
    public static readonly DiagnosticDescriptor OpeningParenthesis = Create(
        "SST1008",
        "Opening parenthesis should be spaced correctly",
        "An opening parenthesis should not be followed by a space",
        "An opening parenthesis is not followed by whitespace on the same line.");

    /// <summary>SST1009 — a closing parenthesis is preceded by a space.</summary>
    public static readonly DiagnosticDescriptor ClosingParenthesis = Create(
        "SST1009",
        "Closing parenthesis should be spaced correctly",
        "A closing parenthesis should not be preceded by a space",
        "A closing parenthesis is not preceded by whitespace.");

    /// <summary>SST1012 — an opening brace is not followed by a space on a single line.</summary>
    public static readonly DiagnosticDescriptor OpeningBrace = Create(
        "SST1012",
        "Opening braces should be spaced correctly",
        "An opening brace should be followed by a space",
        "A non-empty single-line brace block has a space after the opening brace.");

    /// <summary>SST1013 — a closing brace is not preceded by a space on a single line.</summary>
    public static readonly DiagnosticDescriptor ClosingBrace = Create(
        "SST1013",
        "Closing braces should be spaced correctly",
        "A closing brace should be preceded by a space",
        "A non-empty single-line brace block has a space before the closing brace.");

    /// <summary>SST1010 — an opening square bracket is spaced incorrectly (opt-in).</summary>
    public static readonly DiagnosticDescriptor OpeningSquareBracket = CreateOptIn(
        "SST1010",
        "Opening square brackets should be spaced correctly",
        "Fix the spacing at the opening square bracket",
        "An element-access or array '[' has no adjacent whitespace. Collection-expression brackets follow 'stylesharp.collection_expression_spacing' ('none'/'space'). Off by default.");

    /// <summary>SST1023 — a dereference or address-of symbol is followed by a space (opt-in; unsafe pointer code only).</summary>
    public static readonly DiagnosticDescriptor PointerSymbolSpacing = CreateOptIn(
        "SST1023",
        "Dereference and address-of symbols should be spaced correctly",
        "A dereference or address-of symbol should not be followed by a space",
        "A unary dereference ('*') or address-of ('&') symbol touches its operand, with no whitespace after the symbol. Off by default — it applies only to unsafe pointer code.");

    /// <summary>SST1024 — a colon is spaced incorrectly for its context.</summary>
    public static readonly DiagnosticDescriptor ColonSpacing = Create(
        "SST1024",
        "Colons should be spaced correctly",
        "Fix the spacing around the colon",
        "A colon has a space on each side in a base list, ctor initializer, ternary, or constraint; and no space before but one after for labels, named arguments, and attribute targets.");

    /// <summary>SST1007 — an operator keyword is not followed by a space.</summary>
    public static readonly DiagnosticDescriptor OperatorKeywordSpacing = Create(
        "SST1007",
        "Operator keyword should be followed by a space",
        "Add a space after the 'operator' keyword",
        "The 'operator' keyword is followed by a space before the operator symbol or target type.");

    /// <summary>SST1011 — a closing square bracket is spaced incorrectly.</summary>
    public static readonly DiagnosticDescriptor ClosingSquareBracket = Create(
        "SST1011",
        "Closing square brackets should be spaced correctly",
        "Fix the spacing at the closing square bracket",
        "A closing ']' has no preceding whitespace, unless 'stylesharp.collection_expression_spacing = space' selects the padded '[ 1, 2 ]' style.");

    /// <summary>SST1020 — an increment or decrement symbol is separated from its operand by a space.</summary>
    public static readonly DiagnosticDescriptor IncrementDecrementSpacing = Create(
        "SST1020",
        "Increment and decrement symbols should be spaced correctly",
        "An increment or decrement symbol should not be separated from its operand by a space",
        "A '++' or '--' touches the operand it applies to, with no whitespace between them.");

    /// <summary>SST1021 — a unary negative sign is followed by a space.</summary>
    public static readonly DiagnosticDescriptor NegativeSignSpacing = Create(
        "SST1021",
        "Negative signs should be spaced correctly",
        "A unary negative sign should not be followed by a space",
        "A unary '-' touches its operand, with no whitespace after the sign.");

    /// <summary>SST1022 — a unary positive sign is followed by a space.</summary>
    public static readonly DiagnosticDescriptor PositiveSignSpacing = Create(
        "SST1022",
        "Positive signs should be spaced correctly",
        "A unary positive sign should not be followed by a space",
        "A unary '+' touches its operand, with no whitespace after the sign.");

    /// <summary>Creates a Warning-severity Spacing descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Spacing", description);

    /// <summary>Creates a Spacing descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Spacing", description);
}
