// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Diagnostic descriptors for modern C# syntax rules (SST22xx).</summary>
internal static class ModernSyntaxRules
{
    /// <summary>SST2200 — a single-use backing field can use the C# 14 <c>field</c> keyword.</summary>
    public static readonly DiagnosticDescriptor PreferFieldKeyword = CreateOptIn(
        "SST2200",
        "Prefer the field keyword",
        "Replace this single-use backing field with the 'field' keyword",
        "A property with accessor logic uses the C# 14 'field' keyword instead of a private single-use backing field.");

    /// <summary>SST2201 — a return-only switch statement can be written as a switch expression.</summary>
    public static readonly DiagnosticDescriptor PreferSwitchExpression = Create(
        "SST2201",
        "Express return-only switch as a value",
        "Rewrite this return-only switch as a switch expression",
        "A switch that returns or throws from every section is written as a switch expression so the mapping stays expression-shaped and compact.");

    /// <summary>SST2202 — an object creation can use target-typed <c>new</c>.</summary>
    public static readonly DiagnosticDescriptor UseTargetTypedNew = Create(
        "SST2202",
        "Let the target type carry object creation",
        "Remove the repeated object creation type",
        "An object creation repeats the explicit target type and can use target-typed 'new' without changing the constructed type.");

    /// <summary>SST2203 — an index from the end of an array or string can use the C# 8 index operator.</summary>
    public static readonly DiagnosticDescriptor UseIndexOperator = Create(
        "SST2203",
        "Index from the end directly",
        "Replace the length subtraction with a from-end index",
        "An array or string access written as 'value[value.Length - n]' can use the C# 8 index-from-end operator.");

    /// <summary>SST2204 — a string slice can use the C# 8 range operator.</summary>
    public static readonly DiagnosticDescriptor UseRangeOperator = Create(
        "SST2204",
        "Slice strings with range syntax",
        "Replace the substring call with a range expression",
        "A string slice written with Substring can use C# range syntax when the bounds can be rewritten without changing the receiver or argument evaluation.");

    /// <summary>SST2205 — an enum switch statement is missing explicit cases.</summary>
    public static readonly DiagnosticDescriptor CompleteEnumSwitchStatement = Create(
        "SST2205",
        "List every enum switch statement case",
        "Add the missing enum cases to this switch statement",
        "A switch statement over an enum names each enum value explicitly so new and existing cases are visible at the mapping site.");

    /// <summary>SST2206 — an enum switch expression is missing explicit arms.</summary>
    public static readonly DiagnosticDescriptor CompleteEnumSwitchExpression = Create(
        "SST2206",
        "List every enum switch expression arm",
        "Add the missing enum arms to this switch expression",
        "A switch expression over an enum names each enum value explicitly so the expression remains easy to audit.");

    /// <summary>SST2207 — a null guard plus return can use a throw expression.</summary>
    public static readonly DiagnosticDescriptor UseThrowExpression = Create(
        "SST2207",
        "Fold null guard into returned value",
        "Move the throw into the returned expression",
        "A null guard that throws and immediately returns the guarded value can keep that contract in one expression.");

    /// <summary>SST2208 — an out local can be declared at the call site.</summary>
    public static readonly DiagnosticDescriptor InlineOutVariableDeclaration = Create(
        "SST2208",
        "Declare out variables at the call site",
        "Inline this out variable declaration",
        "A local declared solely for the next out argument can be declared in that argument so the temporary stays beside its first write.");

    /// <summary>SST2209 — a null-forgiving operator does not affect the expression.</summary>
    public static readonly DiagnosticDescriptor RemoveUnneededNullForgiving = Create(
        "SST2209",
        "Remove a no-op null-forgiving operator",
        "Remove the null-forgiving operator",
        "A null-forgiving operator on a value already known to be non-null adds noise without changing flow analysis.");

    /// <summary>SST2210 — a nullable directive repeats the current file state.</summary>
    public static readonly DiagnosticDescriptor RemoveRepeatedNullableDirective = Create(
        "SST2210",
        "Remove repeated nullable state",
        "Remove this repeated nullable directive",
        "A nullable directive repeats the same file-local state that is already active.");

    /// <summary>SST2211 — a nullable restore directive has no file-local state to restore.</summary>
    public static readonly DiagnosticDescriptor RemoveUnusedNullableRestore = Create(
        "SST2211",
        "Remove nullable restore with no local effect",
        "Remove this nullable restore directive",
        "A nullable restore directive appears before the file has changed nullable state, so it has no local state to undo.");

    /// <summary>SST2212 — byte data can be written as a UTF-8 string literal.</summary>
    public static readonly DiagnosticDescriptor UseUtf8StringLiteral = Create(
        "SST2212",
        "Use UTF-8 literal bytes",
        "Write these bytes as a UTF-8 string literal",
        "Literal UTF-8 byte data is written with the C# 'u8' suffix so the encoded bytes are visible and produced by the compiler.");

    /// <summary>SST2213 — a discard designation does not add information.</summary>
    public static readonly DiagnosticDescriptor RemoveUnnecessaryDiscard = Create(
        "SST2213",
        "Remove no-op discard designation",
        "Remove the discard designation",
        "A type pattern does not need an explicit discard name; the type alone states the same match.");

    /// <summary>SST2214 — tuple member locals can be declared by deconstruction.</summary>
    public static readonly DiagnosticDescriptor UseDeconstruction = Create(
        "SST2214",
        "Declare tuple parts directly",
        "Deconstruct this tuple into the locals that read it",
        "A tuple stored only so its elements can be copied to locals is declared with deconstruction, keeping the element names beside the source expression.");

    /// <summary>SST2215 — a temporary variable swaps two locals.</summary>
    public static readonly DiagnosticDescriptor UseTupleSwap = Create(
        "SST2215",
        "Swap locals with tuple assignment",
        "Use tuple assignment for this swap",
        "A three-statement local swap can be written as one tuple assignment, removing the temporary without changing evaluation order for local variables.");

    /// <summary>SST2216 — a tuple element name repeats the name C# would infer.</summary>
    public static readonly DiagnosticDescriptor UseInferredTupleElementName = Create(
        "SST2216",
        "Let tuple element names be inferred",
        "Omit the repeated tuple element name",
        "A tuple element name is omitted when the expression already infers the same name.");

    /// <summary>SST2217 — a manual hash-code expression can use <c>System.HashCode.Combine</c>.</summary>
    public static readonly DiagnosticDescriptor UseHashCodeCombine = Create(
        "SST2217",
        "Combine hash inputs with System.HashCode",
        "Use System.HashCode.Combine for this hash expression",
        "A simple multiplier-based GetHashCode expression is written with System.HashCode.Combine when the target framework provides it.");

    /// <summary>SST2218 — explicit lambda parameter types can be inferred from the target delegate.</summary>
    public static readonly DiagnosticDescriptor UseImplicitLambdaParameterTypes = Create(
        "SST2218",
        "Let lambda parameter types come from the target",
        "Remove the repeated lambda parameter types",
        "A lambda with a clear delegate or expression-tree target omits parameter types when the target already supplies them.");

    /// <summary>SST2219 — a simple property accessor body can be expression-bodied.</summary>
    public static readonly DiagnosticDescriptor SimplifyPropertyAccessor = Create(
        "SST2219",
        "Keep simple accessors expression-shaped",
        "Rewrite this accessor as an expression body",
        "A property accessor whose body contains a single return or assignment is written as an expression-bodied accessor.");

    /// <summary>Creates a Warning-severity ModernSyntax descriptor whose help link points at the rule's docs page.</summary>
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
            "ModernSyntax",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>Creates a ModernSyntax descriptor that is disabled by default (opt-in via .editorconfig).</summary>
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
            "ModernSyntax",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
