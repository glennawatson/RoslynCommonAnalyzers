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

    /// <summary>SST2220 — interpolation can carry a simple <c>ToString</c> call directly.</summary>
    public static readonly DiagnosticDescriptor SimplifyInterpolation = Create(
        "SST2220",
        "Let interpolation format the value",
        "Move this ToString call into the interpolation",
        "A string interpolation removes a redundant ToString call and keeps any literal format text in the interpolation hole.");

    /// <summary>SST2221 — an expression statement computes a value that is intentionally ignored.</summary>
    public static readonly DiagnosticDescriptor MakeIgnoredExpressionValueExplicit = CreateOptIn(
        "SST2221",
        "Mark ignored expression values",
        "Assign this ignored value to the discard",
        "An expression statement that produces a value assigns it to the discard so the ignored result is explicit. Off by default because fluent APIs often ignore returned receivers intentionally.");

    /// <summary>SST2222 — a local value is overwritten before it is read.</summary>
    public static readonly DiagnosticDescriptor RemoveOverwrittenValue = Create(
        "SST2222",
        "Remove value overwritten before use",
        "Remove this overwritten value",
        "A local initializer or assignment overwritten by the next write before any read is removed when it has no side effects, as is a postfix step whose written value can never be read.");

    /// <summary>SST2223 — a null check assignment can use <c>??=</c>.</summary>
    public static readonly DiagnosticDescriptor UseCoalesceAssignment = Create(
        "SST2223",
        "Assign fallback values with ??=",
        "Rewrite this null fallback assignment with '??='",
        "A null check that only assigns a fallback value is written with the coalescing assignment operator.");

    /// <summary>SST2224 — an anonymous object can be written as a tuple when the type shape is local style only.</summary>
    public static readonly DiagnosticDescriptor ConvertAnonymousObjectToTuple = CreateOptIn(
        "SST2224",
        "Use tuple syntax for local value bundles",
        "Replace this anonymous object with a tuple literal",
        "A small anonymous object can be written as a tuple literal when the codebase prefers tuple-shaped local value bundles. Off by default because runtime type and equality semantics change.");

    /// <summary>SST2225 — a <c>foreach</c> loop hides an explicit element conversion.</summary>
    public static readonly DiagnosticDescriptor AddExplicitForeachCast = Create(
        "SST2225",
        "Show foreach element casts at the source",
        "Cast the source sequence before this foreach loop",
        "A foreach loop that relies on a runtime element cast makes that cast visible at the sequence expression.");

    /// <summary>SST2226 — a cast hides an inner explicit conversion.</summary>
    public static readonly DiagnosticDescriptor AddVisibleInnerCast = Create(
        "SST2226",
        "Show the inner explicit conversion",
        "Add the hidden inner cast",
        "A source cast that causes another explicit conversion to be emitted makes the inner conversion visible in source.");

    /// <summary>SST2227 — a post-assignment null check can be folded into the assigned expression.</summary>
    public static readonly DiagnosticDescriptor FoldNullCheckIntoAssignment = Create(
        "SST2227",
        "Fold null fallback into the assignment",
        "Move this null fallback into the assigned expression",
        "A value assigned and then immediately checked for null keeps the fallback or throw expression beside the value being assigned.");

    /// <summary>SST2228 — a delegate local initialized with a lambda can be a local function.</summary>
    public static readonly DiagnosticDescriptor UseLocalFunction = Create(
        "SST2228",
        "Prefer a local function for local call sites",
        "Replace this delegate local with a local function",
        "A delegate local that is only invoked in the containing block is written as a local function so the call target has method shape without allocating a delegate.");

    /// <summary>SST2231 — a broad object pattern is only a null check.</summary>
    public static readonly DiagnosticDescriptor UseDirectNullPattern = Create(
        "SST2231",
        "State null checks directly",
        "Replace the broad object pattern with a null pattern",
        "A pattern that only proves a reference is or is not null uses a direct null pattern rather than a broad object type test.");

    /// <summary>SST2232 — <c>nameof</c> does not need concrete generic type arguments.</summary>
    public static readonly DiagnosticDescriptor UseUnboundGenericName = Create(
        "SST2232",
        "Omit generic arguments inside nameof",
        "Replace concrete generic arguments with omitted generic arguments",
        "A nameof expression over a generic type omits concrete type arguments because the generated name does not depend on them.");

    /// <summary>SST2234 — <c>Nullable&lt;T&gt;</c> is written with the question-mark shorthand.</summary>
    public static readonly DiagnosticDescriptor UseNullableShorthand = Create(
        "SST2234",
        "Use the T? shorthand for nullable value types",
        "Replace 'Nullable<{0}>' with '{0}?'",
        "The question-mark shorthand and the explicit Nullable<T> spelling are the same type; the shorthand is shorter and matches how nullable annotations read everywhere else.");

    /// <summary>SST2235 — a capture-free local function can be declared <c>static</c>.</summary>
    public static readonly DiagnosticDescriptor MakeLocalFunctionStatic = Create(
        "SST2235",
        "Make local functions static when they do not capture",
        "Make local function '{0}' static",
        "A local function that does not capture locals, parameters, or instance state is declared static so later edits cannot accidentally introduce a capture.");

    /// <summary>SST2236 — a tail-position <c>using</c> block can use a C# 8 using declaration.</summary>
    public static readonly DiagnosticDescriptor UseUsingDeclaration = Create(
        "SST2236",
        "Use a simple using declaration",
        "Rewrite this tail-position using block as a using declaration",
        "A using block that is already the last statement in its containing block can use a using declaration without extending the disposal lifetime.");

    /// <summary>SST2237 — a single block-scoped namespace can use file-scoped namespace syntax.</summary>
    public static readonly DiagnosticDescriptor UseFileScopedNamespace = Create(
        "SST2237",
        "Use file-scoped namespace syntax",
        "Rewrite namespace '{0}' as a file-scoped namespace",
        "A file with one block-scoped namespace and no sibling declarations can use file-scoped namespace syntax to remove one indentation level.");

    /// <summary>SST2238 — nested property patterns can be flattened with extended property-pattern syntax.</summary>
    public static readonly DiagnosticDescriptor SimplifyNestedPropertyPattern = Create(
        "SST2238",
        "Simplify nested property patterns",
        "Flatten this nested property pattern",
        "A nested property pattern such as '{ P: { Q: value } }' can be written as '{ P.Q: value }' when C# 10 syntax is available.");

    /// <summary>SST2239 — an unambiguous lambda that forwards all arguments to a method can be a method group.</summary>
    public static readonly DiagnosticDescriptor UseMethodGroup = Create(
        "SST2239",
        "Use a method group",
        "Replace this forwarding lambda with a method group",
        "A lambda that only passes its parameters through to one unambiguous method can be written as a method group.");

    /// <summary>SST2240 — a delegate null check followed by invocation can use conditional invocation.</summary>
    public static readonly DiagnosticDescriptor UseConditionalDelegateInvocation = Create(
        "SST2240",
        "Use conditional delegate invocation",
        "Use conditional invocation for this delegate call",
        "A delegate checked for null and then invoked can use '?.Invoke(...)', which evaluates the delegate target once.");

    /// <summary>SST2241 — a constructor that only stores its parameters can become a primary constructor.</summary>
    public static readonly DiagnosticDescriptor UsePrimaryConstructorStorage = Create(
        "SST2241",
        "Move simple constructor storage to the type declaration",
        "Move constructor '{0}' to the type declaration and keep the storage initializers with their members",
        "A constructor whose body only copies parameters into instance fields or properties can use primary-constructor parameters, keeping storage wiring at the type boundary.");

    /// <summary>SST2242 — a switch statement over an enum omits named enum values in a non-catch-all mapping.</summary>
    public static readonly DiagnosticDescriptor CompleteEnumSwitchStatementMapping = Create(
        "SST2242",
        "Enum switch statements should name every mapped value",
        "Add the missing enum values to this switch statement or add an intentional catch-all section",
        "A switch statement that maps enum values without a default section names every enum member explicitly so newly added or forgotten values remain visible.");

    /// <summary>SST2243 — a verbatim string literal is better expressed as a raw string literal.</summary>
    public static readonly DiagnosticDescriptor UseRawStringLiteral = Create(
        "SST2243",
        "Use a raw string literal",
        "Write this string as a raw string literal",
        UseRawStringLiteralDescription);

    /// <summary>SST2244 — a numeric literal's suffix is lower case.</summary>
    public static readonly DiagnosticDescriptor UppercaseLiteralSuffix = Create(
        "SST2244",
        "Numeric literal suffixes should be upper case",
        "Write the '{0}' suffix in upper case",
        UppercaseLiteralSuffixDescription);

    /// <summary>SST2245 — a for loop that only tests a condition should be a while loop.</summary>
    public static readonly DiagnosticDescriptor UseWhileOverFor = Create(
        "SST2245",
        "Use while when a for loop has no initializer or increment",
        "This 'for' has neither an initializer nor an increment; write it as a 'while'",
        UseWhileOverForDescription);

    /// <summary>The SST2243 rule description.</summary>
    private const string UseRawStringLiteralDescription =
        "A verbatim string full of doubled-quote escapes, or one spanning multiple lines, reads with less escape noise as a raw string "
        + "literal while the content stays character-for-character identical. Reported only when the language version supports raw "
        + "strings (C# 11).";

    /// <summary>The UppercaseLiteralSuffix rule description.</summary>
    private const string UppercaseLiteralSuffixDescription =
        "A lower-case 'l' is very nearly a '1' in most fonts, so '1l' reads as eleven. The other suffixes follow the same rule for "
        + "consistency rather than because they are ambiguous.";

    /// <summary>The UseWhileOverFor rule description.</summary>
    private const string UseWhileOverForDescription =
        "'for (; condition; )' is a 'while' with two empty clauses the reader still has to check. Writing 'while (condition)' says the same "
        + "thing with nothing left over.";

    /// <summary>Creates a Warning-severity ModernSyntax descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "ModernSyntax", description);

    /// <summary>Creates a ModernSyntax descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "ModernSyntax", description);
}
