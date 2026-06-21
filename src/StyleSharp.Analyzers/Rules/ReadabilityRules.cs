// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the readability (SST11xx) diagnostic descriptors.
/// </summary>
internal static class ReadabilityRules
{
    /// <summary>SST1106 — a statement is empty (a stray semicolon).</summary>
    public static readonly DiagnosticDescriptor EmptyStatement = Create(
        "SST1106",
        "Code should not contain empty statements",
        "Remove the empty statement",
        "An empty statement (a stray ';') carries no meaning and is removed.");

    /// <summary>SST1107 — more than one statement shares a line.</summary>
    public static readonly DiagnosticDescriptor MultipleStatementsOnLine = Create(
        "SST1107",
        "Code should not contain multiple statements on one line",
        "Place this statement on its own line",
        "Each statement is written on its own line so the control flow is easy to follow.");

    /// <summary>SST1120 — a comment contains no text.</summary>
    public static readonly DiagnosticDescriptor CommentMustContainText = Create(
        "SST1120",
        "Comments should contain text",
        "Remove the empty comment",
        "A comment carries explanatory text rather than being left empty.");

    /// <summary>SST1122 — an empty string literal is used instead of <c>string.Empty</c>.</summary>
    public static readonly DiagnosticDescriptor UseStringEmpty = Create(
        "SST1122",
        "Use string.Empty for empty strings",
        "Replace the empty string literal with 'string.Empty'",
        "An empty string is written as 'string.Empty' rather than \"\" so its intent is explicit.");

    /// <summary>SST1121 — a framework type name is used instead of its built-in alias (opt-in).</summary>
    public static readonly DiagnosticDescriptor UseBuiltInTypeAlias = CreateOptIn(
        "SST1121",
        "Use built-in type alias",
        "Use the built-in alias '{0}' instead of '{1}'",
        "A built-in alias ('int', 'string', …) is used instead of the framework type name ('System.Int32', 'String'). Off by default.");

    /// <summary>SST1125 — a <c>Nullable&lt;T&gt;</c> type is written in long form instead of the <c>T?</c> shorthand.</summary>
    public static readonly DiagnosticDescriptor UseNullableShorthand = Create(
        "SST1125",
        "Use shorthand for nullable types",
        "Use the 'T?' shorthand instead of 'Nullable<T>'",
        "A nullable value type is written as 'T?' rather than the long-form 'Nullable<T>'.");

    /// <summary>SST1129 — a value type is created with a parameterless constructor call instead of <c>default</c>.</summary>
    public static readonly DiagnosticDescriptor DefaultValueTypeConstructor = Create(
        "SST1129",
        "Do not use default value type constructor",
        "Use 'default' instead of a parameterless value-type constructor call",
        "A value type's zero value is written as 'default' rather than a parameterless 'new T()' call.");

    /// <summary>SST1130 — an anonymous delegate is used where a lambda expression is clearer.</summary>
    public static readonly DiagnosticDescriptor UseLambdaSyntax = Create(
        "SST1130",
        "Use lambda syntax",
        "Replace the anonymous delegate with a lambda expression",
        "An inline callback is written as a lambda ('=>') rather than the older 'delegate' syntax.");

    /// <summary>SST1131 — a comparison places the constant on the left ("yoda" condition).</summary>
    public static readonly DiagnosticDescriptor UseReadableConditions = Create(
        "SST1131",
        "Use readable conditions",
        "Place the variable on the left of the comparison and the constant on the right",
        "A comparison reads variable-then-constant ('x == 0') rather than constant-then-variable ('0 == x').");

    /// <summary>SST1123 — a <c>#region</c> is placed inside a code element body.</summary>
    public static readonly DiagnosticDescriptor RegionWithinElement = Create(
        "SST1123",
        "Do not place regions within elements",
        "Move this region outside the code element, or remove it",
        "A '#region' is not placed inside the body of a method, accessor, or other code element.");

    /// <summary>SST1124 — a <c>#region</c> directive is used.</summary>
    public static readonly DiagnosticDescriptor DoNotUseRegions = Create(
        "SST1124",
        "Do not use regions",
        "Remove the region",
        "Regions hide code; well-organised types do not need them.");

    /// <summary>SST1132 — several fields are declared in a single statement.</summary>
    public static readonly DiagnosticDescriptor DoNotCombineFields = Create(
        "SST1132",
        "Do not combine fields",
        "Declare each field in its own statement",
        "Each field is declared on its own so its modifiers and initializer are unambiguous.");

    /// <summary>SST1133 — several attributes share one bracket list.</summary>
    public static readonly DiagnosticDescriptor DoNotCombineAttributes = Create(
        "SST1133",
        "Do not combine attributes",
        "Place each attribute in its own brackets",
        "Each attribute is written in its own '[...]' brackets rather than combined as '[A, B]'.");

    /// <summary>SST1134 — an attribute shares a line with another attribute or the element.</summary>
    public static readonly DiagnosticDescriptor AttributesOnSeparateLines = Create(
        "SST1134",
        "Attributes should not share a line",
        "Place this attribute on its own line",
        "Each attribute bracket list sits on its own line, separate from other attributes and the element.");

    /// <summary>SST1136 — several enum members share a line.</summary>
    public static readonly DiagnosticDescriptor EnumValuesOnSeparateLines = Create(
        "SST1136",
        "Enum values should be on separate lines",
        "Place this enum value on its own line",
        "Each enum member is written on its own line so the members read as a list.");

    /// <summary>SST1137 — sibling elements are indented differently from one another.</summary>
    public static readonly DiagnosticDescriptor ElementsConsistentIndentation = Create(
        "SST1137",
        "Elements should have the same indentation",
        "Indent this element to match its siblings",
        "Sibling members or statements share the same indentation so the block structure is clear.");

    /// <summary>SST1102 — a query clause is separated from the previous clause by a blank line.</summary>
    public static readonly DiagnosticDescriptor QueryClauseFollowsPrevious = Create(
        "SST1102",
        "Query clause should follow the previous clause",
        "Remove the blank line so this query clause follows the previous clause",
        "A query clause begins on the same line as, or the line directly after, the previous clause.");

    /// <summary>SST1103 — query clauses mix single-line and multi-line layout.</summary>
    public static readonly DiagnosticDescriptor QueryClausesConsistentLines = Create(
        "SST1103",
        "Query clauses should be on the same line or each on its own line",
        "Place all query clauses on one line or each on its own line",
        "The clauses of a query are either all on one line or each on its own line, not a mix.");

    /// <summary>SST1104 — a query clause shares the last line of a multi-line previous clause.</summary>
    public static readonly DiagnosticDescriptor QueryClauseOnNewLineAfterMultiLine = Create(
        "SST1104",
        "Query clause should begin on a new line after a multi-line clause",
        "Place this query clause on its own line",
        "When a query clause spans multiple lines, the next clause begins on a new line.");

    /// <summary>SST1105 — a multi-line query clause does not begin on its own line.</summary>
    public static readonly DiagnosticDescriptor QueryClauseMultiLineOwnLine = Create(
        "SST1105",
        "Query clauses spanning multiple lines should begin on their own line",
        "Place this multi-line query clause on its own line",
        "A query clause that spans multiple lines begins on its own line rather than sharing the previous clause's line.");

    /// <summary>SST1100 — a <c>base.</c> prefix is used where the type does not override the member.</summary>
    public static readonly DiagnosticDescriptor DoNotPrefixWithBase = Create(
        "SST1100",
        "Do not prefix calls with base unless a local override exists",
        "Remove the redundant 'base.' prefix",
        "A 'base.' prefix is used only to reach a member the current type overrides; otherwise it is redundant.");

    /// <summary>SST1110 — an opening parenthesis or bracket does not sit on the line of the preceding code.</summary>
    public static readonly DiagnosticDescriptor OpeningParenOnDeclarationLine = Create(
        "SST1110",
        "Opening parenthesis or bracket should be on the declaration line",
        "Place the opening parenthesis or bracket on the line of the preceding code",
        "An opening parenthesis or bracket sits on the same line as the name or keyword that precedes it.");

    /// <summary>SST1111 — a closing parenthesis or bracket does not sit on the last parameter's line.</summary>
    public static readonly DiagnosticDescriptor ClosingParenOnLastParameterLine = Create(
        "SST1111",
        "Closing parenthesis or bracket should be on the line of the last parameter",
        "Place the closing parenthesis or bracket on the line of the last parameter",
        "A closing parenthesis or bracket follows the last parameter on the same line.");

    /// <summary>SST1112 — an empty parameter list's closing parenthesis is on a different line.</summary>
    public static readonly DiagnosticDescriptor ClosingParenOnOpeningLineWhenEmpty = Create(
        "SST1112",
        "Closing parenthesis should be on the line of the opening parenthesis when the list is empty",
        "Place the closing parenthesis on the same line as the opening parenthesis",
        "An empty parameter or argument list keeps its parentheses together on one line.");

    /// <summary>SST1113 — a comma does not sit on the previous parameter's line.</summary>
    public static readonly DiagnosticDescriptor CommaOnPreviousParameterLine = Create(
        "SST1113",
        "Comma should be on the same line as the previous parameter",
        "Place the comma on the same line as the previous parameter",
        "A separating comma follows the previous parameter on the same line, never starting the next line.");

    /// <summary>SST1114 — a blank line separates the declaration from its parameter list.</summary>
    public static readonly DiagnosticDescriptor ParameterListFollowsDeclaration = Create(
        "SST1114",
        "Parameter list should follow the declaration",
        "Remove the blank line between the declaration and the first parameter",
        "The first parameter begins on the line of the opening parenthesis or the line directly after it.");

    /// <summary>SST1115 — a blank line separates a parameter from the preceding comma.</summary>
    public static readonly DiagnosticDescriptor ParameterFollowsComma = Create(
        "SST1115",
        "A parameter should follow the comma without a blank line",
        "Remove the blank line before this parameter",
        "Each parameter begins on the line of the preceding comma or the line directly after it.");

    /// <summary>SST1116 — a qualified name can be shortened without changing the symbol it binds to.</summary>
    public static readonly DiagnosticDescriptor SimplifyName = Create(
        "SST1116",
        "Prefer the shortest symbol-equivalent name",
        "Shorten this qualified name",
        "A qualified type or namespace name is shortened when the shorter name binds to the same symbol.");

    /// <summary>SST1117 — an instance-member access does not match the configured <c>this.</c>-qualification style.</summary>
    public static readonly DiagnosticDescriptor SimplifyMemberAccess = Create(
        "SST1117",
        "Use the configured instance-member qualification",
        "Use the configured instance-member qualification",
        "Instance-member access follows 'stylesharp.instance_member_qualification': StyleSharp omits 'this.' by default, or adds it when the option is set to 'require_this'.");

    /// <summary>SST1118 — a parameter or argument spans multiple lines (opt-in; multi-line callbacks are exempt).</summary>
    public static readonly DiagnosticDescriptor ParameterMustNotSpanMultipleLines = CreateOptIn(
        "SST1118",
        "A parameter should not span multiple lines",
        "Place this parameter on a single line",
        "A parameter or argument fits on one line. Off by default — multi-line lambdas, initializers, and similar callbacks legitimately span lines, and distinguishing them is heuristic.");

    /// <summary>SST1127 — a generic type constraint shares a line with the declaration or another constraint.</summary>
    public static readonly DiagnosticDescriptor ConstraintOnOwnLine = Create(
        "SST1127",
        "Generic type constraints should be on their own line",
        "Place this 'where' constraint clause on its own line",
        "Each 'where' constraint clause is written on its own line below the declaration.");

    /// <summary>SST1128 — a constructor initializer shares a line with the constructor signature.</summary>
    public static readonly DiagnosticDescriptor ConstructorInitializerOnOwnLine = Create(
        "SST1128",
        "Constructor initializers should be on their own line",
        "Place the constructor initializer on its own line",
        "A ': base(...)' or ': this(...)' initializer is written on its own line below the constructor signature.");

    /// <summary>SST1135 — a using directive names a namespace or type that is not fully qualified.</summary>
    public static readonly DiagnosticDescriptor UsingDirectiveQualified = Create(
        "SST1135",
        "Using directives should be qualified",
        "Qualify the using directive as '{0}'",
        "A using directive names the namespace or type in fully qualified form so it does not depend on context.");

    /// <summary>SST1139 — a numeric literal is cast where a literal suffix would express the type.</summary>
    public static readonly DiagnosticDescriptor UseLiteralSuffix = Create(
        "SST1139",
        "Use literal suffix notation instead of casting",
        "Use the literal suffix '{0}' instead of a cast",
        "A typed numeric literal uses a suffix ('1L', '2.0f') rather than a cast applied to an untyped literal.");

    /// <summary>SST1140 — wrapped conditional operators should start an indented continuation line.</summary>
    public static readonly DiagnosticDescriptor ConditionalOperatorIndentedLine = Create(
        "SST1140",
        "Start conditional operators on indented continuation lines",
        "Place the '{0}' operator at the start of an indented continuation line",
        "A wrapped conditional expression places '?' and ':' at the start of one-step-indented continuation lines so the two branches line up.");

    /// <summary>SST1141 — an explicit <c>ValueTuple&lt;...&gt;</c> is used where tuple syntax would do.</summary>
    public static readonly DiagnosticDescriptor UseTupleSyntax = Create(
        "SST1141",
        "Use tuple syntax",
        "Use tuple syntax instead of the ValueTuple<...> type",
        "A value tuple type is written with the language tuple syntax '(T1, T2)' rather than the underlying ValueTuple<...> type.");

    /// <summary>SST1142 — a tuple element is accessed by <c>ItemN</c> where it has a name.</summary>
    public static readonly DiagnosticDescriptor ReferToTupleElementByName = Create(
        "SST1142",
        "Refer to tuple elements by name",
        "Refer to the tuple element as '{0}'",
        "A named tuple element is accessed by its name rather than the positional 'ItemN' field.");

    /// <summary>SST1143 — a boolean expression is compared to a <c>true</c>/<c>false</c> literal (StyleSharp original).</summary>
    public static readonly DiagnosticDescriptor NoBooleanLiteralComparison = Create(
        "SST1143",
        "Do not compare to a boolean literal",
        "Remove the redundant comparison to '{0}'",
        "A boolean expression is used directly rather than compared to the 'true' or 'false' literal.");

    /// <summary>SST1144 — stacked case labels could be combined into one <c>or</c> pattern (StyleSharp original; opt-in).</summary>
    public static readonly DiagnosticDescriptor PreferOrPattern = CreateOptIn(
        "SST1144",
        "Combine case labels with an or-pattern",
        "Combine these case labels into a single 'or' pattern",
        "Stacked case labels are combined into one 'case A or B:' pattern (C# 9+). Off by default — stacked labels are a common, valid style.");

    /// <summary>SST1145 — a wrapped conditional expression places <c>?</c>/<c>:</c> inconsistently (StyleSharp original).</summary>
    public static readonly DiagnosticDescriptor ConditionalOperatorPlacement = Create(
        "SST1145",
        "Place conditional operators consistently",
        "Place the '{0}' operator at the {1} of the line",
        "When a conditional expression wraps, its '?' and ':' operators are placed consistently (leading by default); set 'stylesharp.conditional_operator_placement' in .editorconfig.");

    /// <summary>SST1146 — an <c>if</c> statement follows a closing brace on the same line.</summary>
    public static readonly DiagnosticDescriptor ConditionalOnNewLine = Create(
        "SST1146",
        "An if statement should start on a new line",
        "Move the 'if' statement to a new line",
        "An independent 'if' statement starts on a new line after the preceding closing brace.");

    /// <summary>SST1147 — a conditional expression is nested inside another conditional expression (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoNestedTernary = CreateOptIn(
        "SST1147",
        "Do not nest conditional operators",
        "Extract this nested conditional expression into an independent statement",
        "Nested conditional expressions are difficult to scan. Off by default because shallow conditional chains are a defensible style.");

    /// <summary>SST1148 — a regular comment appears to contain C# code (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoCommentedOutCode = CreateOptIn(
        "SST1148",
        "Commented-out code should be removed",
        "This comment resembles commented-out code ({0}); remove it, or reword it if it is prose",
        "Source control preserves old code; comments are for explanation. Detection is a shape heuristic — prose ending in ';', '{', or '}', or starting with a keyword can trip it. Off by default.");

    /// <summary>SST1149 — a null check uses <c>== null</c> / <c>!= null</c> instead of the pattern form.</summary>
    public static readonly DiagnosticDescriptor PreferIsNullPattern = Create(
        "SST1149",
        "Prefer the 'is null' pattern for null checks",
        "Use '{0}' for this null check",
        "A null check is written as 'x is null' or 'x is not null' rather than 'x == null' or 'x != null', which reads directly and ignores overloaded equality operators.");

    /// <summary>SST1172 — a comparison is wrapped in a logical-not (<c>!(a == b)</c>) instead of using the opposite operator.</summary>
    public static readonly DiagnosticDescriptor NoInvertedBooleanCheck = Create(
        "SST1172",
        "Negated comparisons should use the opposite operator",
        "Drop the '!' and write this comparison with '{0}'",
        "A comparison negated with '!' reads more directly using the opposite operator: '!(a == b)' becomes 'a != b'. Relational forms are flagged only for non-nullable, non-float operands.");

    /// <summary>SST1173 — an anonymous-type member restates a name that would be inferred (<c>new { X = obj.X }</c>).</summary>
    public static readonly DiagnosticDescriptor NoRedundantAnonymousTypeMemberName = Create(
        "SST1173",
        "Redundant anonymous-type member names should be omitted",
        "Omit the redundant member name '{0}'",
        "When an anonymous-type member is assigned from a member or variable of the same name, the name is inferred and can be omitted: 'new { obj.X }' instead of 'new { X = obj.X }'.");

    /// <summary>SST1174 — a <c>return;</c> or <c>continue;</c> at the tail of its block has no effect.</summary>
    public static readonly DiagnosticDescriptor NoRedundantJump = Create(
        "SST1174",
        "Redundant jump statements should be removed",
        "Remove this redundant '{0}' statement; control already continues here",
        "A 'return;' at the end of a void member or a 'continue;' at the end of a loop body does nothing, because control already flows to the same place.");

    /// <summary>SST1175 — a cast targets the type the operand already has (<c>(int)anInt</c>).</summary>
    public static readonly DiagnosticDescriptor NoRedundantCast = Create(
        "SST1175",
        "Unnecessary casts should be removed",
        "Drop the unnecessary cast to '{0}'; the operand already has that type",
        "A cast whose target type (including nullability) matches the operand's own type does nothing and only adds noise.");

    /// <summary>SST1176 — a field, event, or auto-property is initialized to the type's default (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoMemberInitializedToDefault = CreateOptIn(
        "SST1176",
        "Members should not be initialized to their default value",
        "Drop the initializer; '{0}' already starts at its default value",
        "Fields, events, and auto-properties already start at the type default, so '= 0', '= false', '= null', and '= default' add nothing. Off by default; explicit defaults are a defensible style.");

    /// <summary>SST1177 — a base list restates a type the compiler already implies (<c>class C : object</c>).</summary>
    public static readonly DiagnosticDescriptor NoRedundantInheritanceList = Create(
        "SST1177",
        "Redundant base types should be removed",
        "Remove the redundant base type '{0}'; it is already implied",
        "Listing 'object' as a base type, or 'int' as an enum's underlying type, restates the compiler default and can be removed.");

    /// <summary>SST1178 — a constructor calls <c>: base()</c> with no arguments, which the compiler already emits.</summary>
    public static readonly DiagnosticDescriptor NoRedundantBaseConstructorCall = Create(
        "SST1178",
        "Redundant base constructor calls should be removed",
        "Remove the redundant ': base()' call; the compiler already calls the base constructor",
        "A parameterless ': base()' initializer restates what the compiler emits automatically and can be removed.");

    /// <summary>SST1179 — a <c>default:</c> switch section contains only <c>break;</c>, matching the implicit fall-through.</summary>
    public static readonly DiagnosticDescriptor NoRedundantDefaultSwitchSection = Create(
        "SST1179",
        "Redundant default switch sections should be removed",
        "Remove the redundant 'default' section; it only breaks, like the absent default",
        "A 'default:' section whose only statement is 'break;' behaves the same as having no default section, so it adds nothing.");

    /// <summary>SST1180 — an <c>else</c> clause has an empty body (<c>else { }</c> or <c>else ;</c>).</summary>
    public static readonly DiagnosticDescriptor NoEmptyElseClause = Create(
        "SST1180",
        "Empty else clauses should be removed",
        "Remove the empty 'else' clause; it has no effect",
        "An 'else' clause whose body is empty does nothing and only adds noise to the 'if' statement.");

    /// <summary>SST1181 — an override does nothing but forward to the same base member with the same arguments.</summary>
    public static readonly DiagnosticDescriptor NoRedundantOverride = Create(
        "SST1181",
        "Redundant overriding members should be removed",
        "Remove the redundant override of '{0}'; it only forwards to the base member",
        "An override that does nothing but call the base member with the same arguments has no effect and can be deleted so the inherited member is used directly.");

    /// <summary>SST1182 — a conditional expression yields the boolean literals <c>true</c>/<c>false</c> for its branches.</summary>
    public static readonly DiagnosticDescriptor NoConditionalBooleanLiteral = Create(
        "SST1182",
        "Conditional expressions should not just return boolean literals",
        "Use the condition directly instead of returning 'true'/'false' from a conditional expression",
        "A 'c ? true : false' yields the condition itself, and 'c ? false : true' yields its negation, so the conditional expression is unnecessary.");

    /// <summary>SST1183 — an interpolated string has no interpolations and is just a constant string.</summary>
    public static readonly DiagnosticDescriptor NoRedundantInterpolatedString = Create(
        "SST1183",
        "Interpolated strings without interpolations should be plain strings",
        "Remove the '$' prefix; this interpolated string contains no interpolations",
        "An interpolated string with no '{...}' holes is a constant string, so the '$' prefix only adds noise.");

    /// <summary>SST1184 — a verbatim string literal contains no characters that need verbatim quoting.</summary>
    public static readonly DiagnosticDescriptor NoRedundantVerbatimString = Create(
        "SST1184",
        "Verbatim string literals without escapes should be regular strings",
        "Remove the '@' prefix; this verbatim string needs no verbatim quoting",
        "A verbatim ('@') string whose text contains no backslash, embedded quote, or line break reads the same as a regular string literal, so the '@' adds nothing.");

    /// <summary>SST1185 — an assignment recomputes its own target (<c>x = x + y</c>) instead of using a compound operator.</summary>
    public static readonly DiagnosticDescriptor UseCompoundAssignment = Create(
        "SST1185",
        "Use a compound assignment operator",
        "Use the '{0}' compound assignment operator",
        "An assignment of the form 'x = x op y' reads more directly as the compound assignment 'x op= y'.");

    /// <summary>SST1186 — a literal sits on the left of a comparison (<c>0 == count</c>) rather than the right.</summary>
    public static readonly DiagnosticDescriptor LiteralOnRightOfComparison = Create(
        "SST1186",
        "A literal should be on the right side of a comparison",
        "Place the literal on the right side of the comparison",
        "A comparison reads more naturally with the variable first and the literal second ('count == 0' rather than '0 == count').");

    /// <summary>SST1187 — an assignment is used as the value of another assignment (<c>a = b = c</c>).</summary>
    public static readonly DiagnosticDescriptor NoChainedAssignment = Create(
        "SST1187",
        "Assignments should not be chained",
        "Split this chained assignment into separate statements",
        "Chaining assignments ('a = b = c') hides one of the writes; writing each assignment on its own statement makes both targets obvious.");

    /// <summary>SST1188 — a default expression names its type (<c>default(T)</c>) where the bare <c>default</c> literal suffices.</summary>
    public static readonly DiagnosticDescriptor UseDefaultLiteral = Create(
        "SST1188",
        "Use the 'default' literal instead of 'default(T)'",
        "Use the 'default' literal; the target type is already known",
        "When the target type is inferred from context, the 'default' literal is clearer than the explicit 'default(T)' form.");

    /// <summary>SST1189 — an assignment copies a value onto itself (<c>x = x</c>), which has no effect.</summary>
    public static readonly DiagnosticDescriptor NoSelfAssignment = Create(
        "SST1189",
        "Variables should not be self-assigned",
        "Remove this self-assignment of '{0}'; it has no effect",
        "Assigning a variable, field, or property to itself does nothing and usually signals a typo where a different target or source was meant.");

    /// <summary>SST1190 — a value is negated twice (<c>!!x</c>, <c>~~x</c>), which cancels out.</summary>
    public static readonly DiagnosticDescriptor NoDoubledNegation = Create(
        "SST1190",
        "Doubled negation operators should be removed",
        "Remove the doubled '{0}' operator; the two cancel out",
        "Applying the same prefix-negation operator twice ('!!x', '~~x') returns the original value, so the pair only adds noise.");

    /// <summary>SST1191 — a long numeric literal has no digit separators (opt-in).</summary>
    public static readonly DiagnosticDescriptor UseDigitSeparators = CreateOptIn(
        "SST1191",
        "Long numeric literals should use digit separators",
        "Add digit separators to '{0}' so the magnitude is easy to read",
        "A numeric literal with many digits is easier to read in groups ('1_000_000' rather than '1000000'). Off by default; grouping style is a matter of taste.");

    /// <summary>SST1192 — a string literal embeds a non-printable control character verbatim (opt-in).</summary>
    public static readonly DiagnosticDescriptor EscapeControlCharacters = CreateOptIn(
        "SST1192",
        "Control characters in string literals should be escaped",
        "Replace the embedded control character with an explicit escape sequence",
        "A literal that embeds a raw tab or other control character is easy to misread or alter by accident; an explicit escape ('\\t') states the intent. Off by default.");

    /// <summary>SST1193 — object setup after construction can use an object initializer.</summary>
    public static readonly DiagnosticDescriptor UseObjectInitializer = Create(
        "SST1193",
        "Keep initial member values with construction",
        "Move the first assignment for '{0}' into the initializer",
        "A new object followed immediately by member assignments is written with an object initializer so construction and initialization stay together.");

    /// <summary>SST1194 — collection setup after construction can use a collection initializer.</summary>
    public static readonly DiagnosticDescriptor UseCollectionInitializer = Create(
        "SST1194",
        "Keep initial collection values with construction",
        "Move the first Add call for '{0}' into the initializer",
        "A new collection followed immediately by Add calls is written with a collection initializer so its initial contents stay with the construction.");

    /// <summary>SST1195 — a null conditional can use the null-coalescing operator.</summary>
    public static readonly DiagnosticDescriptor UseNullCoalescingExpression = Create(
        "SST1195",
        "Write null fallback with ??",
        "Write this fallback with '??'",
        "A conditional expression that returns the same value when it is non-null and a fallback otherwise is written with the null-coalescing operator.");

    /// <summary>SST1196 — a null guard before member access can use null propagation.</summary>
    public static readonly DiagnosticDescriptor UseNullPropagation = Create(
        "SST1196",
        "Write null-guarded access directly",
        "Write this guarded access with '?.'",
        "A conditional expression that returns null when the receiver is null and otherwise reads a member is written with the null-propagation operator.");

    /// <summary>SST1197 — adjacent return statements can use a conditional expression.</summary>
    public static readonly DiagnosticDescriptor UseConditionalExpressionForReturn = Create(
        "SST1197",
        "Collapse return-only branch into one return",
        "Collapse this branch into one conditional return",
        "An if statement that only chooses between two return values is written as one return with a conditional expression.");

    /// <summary>SST1198 — matching assignments can use a conditional expression.</summary>
    public static readonly DiagnosticDescriptor UseConditionalExpressionForAssignment = Create(
        "SST1198",
        "Collapse assignment-only branch into one assignment",
        "Collapse this branch into one conditional assignment",
        "An if/else statement that only assigns one target from two values is written as one assignment with a conditional expression.");

    /// <summary>SST1199 — <c>typeof(T).Name</c> can use <c>nameof(T)</c>.</summary>
    public static readonly DiagnosticDescriptor UseNameofType = Create(
        "SST1199",
        "Prefer compile-time type names",
        "Replace 'typeof({0}).Name' with 'nameof({0})'",
        "A type name is written with 'nameof(T)' instead of 'typeof(T).Name' so the code stays compile-time only.");

    /// <summary>Creates a Warning-severity Readability descriptor whose help link points at the rule's docs page.</summary>
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
            "Readability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>Creates a Readability descriptor that is disabled by default (opt-in via .editorconfig).</summary>
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
            "Readability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
