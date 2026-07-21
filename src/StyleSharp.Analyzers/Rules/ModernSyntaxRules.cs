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

    /// <summary>SST2246 — a same-subject conditional chain can be a switch expression.</summary>
    public static readonly DiagnosticDescriptor ConvertChainedConditionalToSwitch = Create(
        "SST2246",
        "Express a same-value conditional chain as a switch",
        "Rewrite this conditional chain that tests one value against constants as a switch expression",
        ConvertChainedConditionalToSwitchDescription);

    /// <summary>SST2247 — consecutive locals copying one value's members can use deconstruction.</summary>
    public static readonly DiagnosticDescriptor DeconstructMemberCopies = Create(
        "SST2247",
        "Deconstruct a value instead of copying its members",
        "Deconstruct '{0}' into these locals instead of copying its members one at a time",
        DeconstructMemberCopiesDescription);

    /// <summary>SST2248 — comparisons of one value against constants can fold into a single is-pattern.</summary>
    public static readonly DiagnosticDescriptor UseComparisonPattern = Create(
        "SST2248",
        "Combine comparisons into a pattern",
        "Combine these comparisons into a single is-pattern",
        UseComparisonPatternDescription);

    /// <summary>SST2249 — a composite format call or a literal-plus-value concatenation reads more clearly as interpolation.</summary>
    public static readonly DiagnosticDescriptor UseInterpolatedString = Create(
        "SST2249",
        "Use an interpolated string",
        "Rewrite this {0} as an interpolated string",
        UseInterpolatedStringDescription);

    /// <summary>SST2250 — a bare local declaration and its immediately following first assignment can be joined.</summary>
    public static readonly DiagnosticDescriptor JoinDeclarationAndAssignment = Create(
        "SST2250",
        "Join a declaration with its first assignment",
        "Give '{0}' its first value at the declaration",
        JoinDeclarationAndAssignmentDescription);

    /// <summary>SST2251 — an explicit method type-argument list repeats what inference would supply.</summary>
    public static readonly DiagnosticDescriptor OmitInferableTypeArguments = Create(
        "SST2251",
        "Let inference supply the type arguments",
        "Remove the explicit type arguments that inference supplies unchanged",
        OmitInferableTypeArgumentsDescription);

    /// <summary>SST2252 — a switch statement is nested inside another switch statement's section.</summary>
    public static readonly DiagnosticDescriptor AvoidNestedSwitchStatement = Create(
        "SST2252",
        "Avoid nesting switch statements",
        "Extract this nested switch statement into its own method, a switch expression, or a lookup",
        AvoidNestedSwitchStatementDescription);

    /// <summary>SST2254 — a target-typed <c>new</c> can instead name the created type explicitly.</summary>
    public static readonly DiagnosticDescriptor UseExplicitObjectCreationType = CreateOptIn(
        "SST2254",
        "Name the created type in an object creation",
        "Name the created type '{0}' in this object creation instead of a target-typed 'new'",
        UseExplicitObjectCreationTypeDescription);

    /// <summary>SST2255 — a null-or-empty string test is written out by hand.</summary>
    public static readonly DiagnosticDescriptor UseIsNullOrEmpty = Create(
        "SST2255",
        "Use string.IsNullOrEmpty",
        "Replace this hand-written null-or-empty test with 'string.IsNullOrEmpty'",
        UseIsNullOrEmptyDescription);

    /// <summary>SST2256 — a static-form extension call reads better in instance form.</summary>
    public static readonly DiagnosticDescriptor UseInstanceExtensionInvocation = CreateInfo(
        "SST2256",
        "Call extension methods in instance form",
        "Call '{0}' in instance form on its first argument",
        UseInstanceExtensionInvocationDescription);

    /// <summary>SST2257 — a lambda block body is a single return statement.</summary>
    public static readonly DiagnosticDescriptor SimplifyLambdaBody = CreateInfo(
        "SST2257",
        "Keep single-return lambdas expression-shaped",
        "Rewrite this lambda's block body as an expression body",
        SimplifyLambdaBodyDescription);

    /// <summary>SST2258 — a delegate is wrapped in an explicit delegate creation the target already supplies.</summary>
    public static readonly DiagnosticDescriptor RemoveRedundantDelegateCreation = CreateInfo(
        "SST2258",
        "Drop a redundant delegate wrapper",
        "Remove the explicit '{0}' wrapper and use the method group directly",
        RemoveRedundantDelegateCreationDescription);

    /// <summary>SST2259 — a stray empty statement follows a declaration.</summary>
    public static readonly DiagnosticDescriptor RemoveStrayEmptyStatement = CreateInfo(
        "SST2259",
        "Remove a stray semicolon after a declaration",
        "Remove this stray ';' after the declaration",
        RemoveStrayEmptyStatementDescription);

    /// <summary>SST2260 — an <c>as</c> cast targets a type the operand already has.</summary>
    public static readonly DiagnosticDescriptor RemoveRedundantAsCast = CreateInfo(
        "SST2260",
        "Remove an 'as' cast to a type the value already has",
        "Remove the 'as {0}' cast; the value already has that type",
        RemoveRedundantAsCastDescription);

    /// <summary>SST2261 — an exclusive-or is spelled out with <c>&amp;&amp;</c>/<c>||</c> (or <c>&amp;</c>/<c>|</c>).</summary>
    public static readonly DiagnosticDescriptor UseExclusiveOr = CreateInfo(
        "SST2261",
        "Use the exclusive-or operator",
        "Replace this exclusive-or reimplementation with '^'",
        UseExclusiveOrDescription);

    /// <summary>SST2262 — a raw string literal carries no content raw syntax is for.</summary>
    public static readonly DiagnosticDescriptor UseRegularStringLiteral = CreateInfo(
        "SST2262",
        "Use a regular string literal",
        "Write this raw string literal as a regular \"...\" literal",
        UseRegularStringLiteralDescription);

    /// <summary>SST2263 — an infinite loop guards its body with a condition that belongs in the header.</summary>
    public static readonly DiagnosticDescriptor HoistLoopCondition = CreateInfo(
        "SST2263",
        "Hoist a loop's guard condition into its header",
        "Move this loop's guard condition into a 'while' header",
        HoistLoopConditionDescription);

    /// <summary>SST2264 — a numeric literal is cast to an enum instead of naming the member.</summary>
    public static readonly DiagnosticDescriptor UseNamedEnumMember = Create(
        "SST2264",
        "Name the enum member instead of casting a number",
        "Replace the numeric cast with '{0}'",
        UseNamedEnumMemberDescription);

    /// <summary>SST2265 — consecutive fluent calls on one receiver can fold into a chain.</summary>
    public static readonly DiagnosticDescriptor FoldFluentCallChain = CreateOptIn(
        "SST2265",
        "Fold consecutive fluent calls into one chain",
        "Fold these consecutive calls on '{0}' into a single fluent chain",
        FoldFluentCallChainDescription);

    /// <summary>SST2266 — a local read exactly once can be inlined into that use.</summary>
    public static readonly DiagnosticDescriptor InlineSingleUseLocal = CreateOptIn(
        "SST2266",
        "Inline a local that is read once",
        "Inline '{0}' into its single use",
        InlineSingleUseLocalDescription);

    /// <summary>SST2267 — an infinite loop is written in the non-configured style.</summary>
    public static readonly DiagnosticDescriptor NormalizeInfiniteLoopStyle = CreateOptIn(
        "SST2267",
        "Write infinite loops in one consistent style",
        "Write this infinite loop as '{0}'",
        NormalizeInfiniteLoopStyleDescription);

    /// <summary>SST2268 — an object creation with an initializer carries the non-configured parentheses style.</summary>
    public static readonly DiagnosticDescriptor NormalizeObjectCreationParentheses = CreateOptIn(
        "SST2268",
        "Keep object-creation parentheses consistent",
        "Write this object creation in the configured '{0}' parentheses style",
        NormalizeObjectCreationParenthesesDescription);

    /// <summary>SST2269 — a conditional condition carries the non-configured parentheses style.</summary>
    public static readonly DiagnosticDescriptor NormalizeConditionalConditionParentheses = CreateOptIn(
        "SST2269",
        "Keep conditional-condition parentheses consistent",
        "Write this conditional condition in the configured '{0}' parentheses style",
        NormalizeConditionalConditionParenthesesDescription);

    /// <summary>SST2270 — an array creation names its element type in the non-configured style.</summary>
    public static readonly DiagnosticDescriptor NormalizeArrayCreationTypeStyle = CreateOptIn(
        "SST2270",
        "Keep array-creation element types consistent",
        "Write this array creation with an {0} element type",
        NormalizeArrayCreationTypeStyleDescription);

    /// <summary>SST2271 — a local (or foreach) variable type does not match the configured var style.</summary>
    public static readonly DiagnosticDescriptor NormalizeVarStyle = CreateOptIn(
        "SST2271",
        "Keep the var-versus-explicit choice consistent",
        "Use '{0}' for this variable declaration",
        NormalizeVarStyleDescription);

    /// <summary>SST2272 — a Flags-enum single-bit member value is written in the non-configured style.</summary>
    public static readonly DiagnosticDescriptor NormalizeEnumFlagValueStyle = CreateOptIn(
        "SST2272",
        "Keep Flags-enum values in one consistent style",
        "Write this flag value as '{0}'",
        NormalizeEnumFlagValueStyleDescription);

    /// <summary>SST2273 — a trailing wrapping <c>if</c> can be inverted into an early-exit guard clause.</summary>
    public static readonly DiagnosticDescriptor PreferGuardClause = CreateOptIn(
        "SST2273",
        "Prefer a guard clause over a trailing wrapping if",
        "Invert this trailing 'if' into an early-exit guard so the wrapped work runs at the outer level",
        PreferGuardClauseDescription);

    /// <summary>SST2274 — an <c>as</c> assignment paired with a null check can use an <c>is</c> declaration pattern.</summary>
    public static readonly DiagnosticDescriptor ConvertAsAssignmentToIsPattern = Create(
        "SST2274",
        "Convert an as assignment and null check to an is pattern",
        "Match '{0}' with an 'is' pattern instead of an 'as' assignment and a separate null check",
        ConvertAsAssignmentToIsPatternDescription);

    /// <summary>SST2275 — a single-statement method body can be an expression body.</summary>
    public static readonly DiagnosticDescriptor UseExpressionBodyForMethod = CreateInfo(
        "SST2275",
        "Keep a single-statement method expression-shaped",
        "Rewrite this method's block body as an expression body",
        UseExpressionBodyForMethodDescription);

    /// <summary>SST2276 — a single-call constructor body can be an expression body.</summary>
    public static readonly DiagnosticDescriptor UseExpressionBodyForConstructor = CreateOptIn(
        "SST2276",
        "Keep a single-statement constructor expression-shaped",
        "Rewrite this constructor's block body as an expression body",
        UseExpressionBodyForConstructorDescription);

    /// <summary>SST2277 — a single-return operator body can be an expression body.</summary>
    public static readonly DiagnosticDescriptor UseExpressionBodyForOperator = CreateOptIn(
        "SST2277",
        "Keep a single-return operator expression-shaped",
        "Rewrite this operator's block body as an expression body",
        UseExpressionBodyForOperatorDescription);

    /// <summary>SST2278 — a single-return conversion operator body can be an expression body.</summary>
    public static readonly DiagnosticDescriptor UseExpressionBodyForConversionOperator = CreateOptIn(
        "SST2278",
        "Keep a single-return conversion operator expression-shaped",
        "Rewrite this conversion operator's block body as an expression body",
        UseExpressionBodyForConversionOperatorDescription);

    /// <summary>SST2279 — a get-only property with a single-return getter can be an expression body.</summary>
    public static readonly DiagnosticDescriptor UseExpressionBodyForProperty = CreateInfo(
        "SST2279",
        "Keep a get-only property expression-shaped",
        "Rewrite this get-only property as an expression body",
        UseExpressionBodyForPropertyDescription);

    /// <summary>SST2280 — a get-only indexer with a single-return getter can be an expression body.</summary>
    public static readonly DiagnosticDescriptor UseExpressionBodyForIndexer = CreateInfo(
        "SST2280",
        "Keep a get-only indexer expression-shaped",
        "Rewrite this get-only indexer as an expression body",
        UseExpressionBodyForIndexerDescription);

    /// <summary>SST2281 — a single-statement local function body can be an expression body.</summary>
    public static readonly DiagnosticDescriptor UseExpressionBodyForLocalFunction = CreateInfo(
        "SST2281",
        "Keep a single-statement local function expression-shaped",
        "Rewrite this local function's block body as an expression body",
        UseExpressionBodyForLocalFunctionDescription);

    /// <summary>SST2282 — a <c>ReferenceEquals</c> check against <see langword="null"/> reads more directly as an is-null pattern.</summary>
    public static readonly DiagnosticDescriptor UseNullPatternOverReferenceEquals = Create(
        "SST2282",
        "Use an is-null pattern instead of ReferenceEquals against null",
        "Use '{0}' instead of a 'ReferenceEquals' check against null",
        UseNullPatternOverReferenceEqualsDescription);

    /// <summary>SST2283 — a null guard that throws before assigning the guarded value can fold into a throw expression.</summary>
    public static readonly DiagnosticDescriptor FoldGuardIntoAssignedValue = Create(
        "SST2283",
        "Fold a preceding null guard into the assigned value",
        "Fold this null guard into the following assignment as a throw expression",
        FoldGuardIntoAssignedValueDescription);

    /// <summary>The SST2255 rule description.</summary>
    private const string UseIsNullOrEmptyDescription =
        "A disjunction such as 'value == null || value.Length == 0' — or the '== \"\"' variant, or the negated "
        + "'value != null && value.Length != 0' — is the null-or-empty test 'string.IsNullOrEmpty' already names. "
        + "The helper reads at a glance, evaluates the value once instead of twice, and cannot get the short-circuit "
        + "order wrong. Reported only when the checked value is a side-effect-free string expression, so folding the "
        + "two reads into one changes nothing.";

    /// <summary>The SST2256 rule description.</summary>
    private const string UseInstanceExtensionInvocationDescription =
        "An extension method called in static form — 'Type.Extension(receiver, arg)' — hides that 'receiver' is the "
        + "value the method extends. Calling it as 'receiver.Extension(arg)' puts the receiver where the eye expects "
        + "it and lets the call chain read left to right. Reported only when the first argument binds to the "
        + "extension's 'this' parameter and the instance form provably resolves to the same method, which the fix "
        + "confirms before it is offered.";

    /// <summary>The SST2257 rule description.</summary>
    private const string SimplifyLambdaBodyDescription =
        "A lambda whose block body is a single 'return expr;' — 'x => { return expr; }' — wraps one expression in a "
        + "block, a return keyword, and a pair of braces. The expression-bodied form 'x => expr' says the same thing "
        + "with none of the ceremony. The rewrite is pure syntax and never changes what the lambda computes.";

    /// <summary>The SST2258 rule description.</summary>
    private const string RemoveRedundantDelegateCreationDescription =
        "Subscribing or assigning with 'Changed += new EventHandler(OnChanged)' wraps a method group in an explicit "
        + "delegate creation the target type already supplies. 'Changed += OnChanged' converts the same method group "
        + "to the same delegate with nothing spelled out. Reported only when the wrapped argument is a method group "
        + "and dropping the wrapper provably binds to the same delegate, which the fix confirms before it is offered.";

    /// <summary>The SST2259 rule description.</summary>
    private const string RemoveStrayEmptyStatementDescription =
        "A declaration that already ends in a brace body — 'class Foo { };' or 'void M() { };' — can carry a trailing "
        + "';' the grammar permits but nothing needs. The semicolon states an empty statement that does nothing and "
        + "only invites the reader to wonder what it is for. The fix removes it and keeps the surrounding trivia.";

    /// <summary>The SST2260 rule description.</summary>
    private const string RemoveRedundantAsCastDescription =
        "An 'as' conversion to a type the operand already has — 'value as string' where 'value' is statically a "
        + "'string' — can never return null and never changes the value or its type. It only adds a conversion the "
        + "reader has to check. The fix drops the 'as' and keeps the operand. Reported only when the operand's static "
        + "type is exactly the target type.";

    /// <summary>The SST2261 rule description.</summary>
    private const string UseExclusiveOrDescription =
        "'(x && !y) || (!x && y)' — and the bitwise '(x & !y) | (!x & y)' — is exclusive-or written the long way, "
        + "evaluating each operand twice and asking the reader to confirm the two conjuncts are mirror images. "
        + "'x ^ y' says it once. Reported only when 'x' and 'y' are side-effect-free boolean expressions, because the "
        + "long form reads each of them twice and '^' reads each once; a value with a side effect is left alone.";

    /// <summary>The SST2262 rule description.</summary>
    private const string UseRegularStringLiteralDescription =
        "A single-line raw string literal whose content holds no quote and no backslash — '\"\"\"plain text\"\"\"' — "
        + "buys nothing over a regular '\"plain text\"' literal, which is what raw syntax exists to avoid escaping. "
        + "The regular literal is shorter and needs no escapes because the content has none to escape. The inverse of "
        + "promoting a literal to raw syntax when doubled quotes or line breaks are what make raw pay off.";

    /// <summary>The SST2263 rule description.</summary>
    private const string HoistLoopConditionDescription =
        "'while (true)' and 'for (;;)' whose body is 'if (cond) { work } else break;' — or a leading 'if (!cond) break;' "
        + "guard before the work — spend the loop header on 'true' and then re-derive the real stopping condition "
        + "inside the body. Moving that condition into the header, 'while (cond) { work }', puts the loop's exit where "
        + "a reader looks for it. Reported only for the condition-hoist shape; an empty guarded body or an else-only "
        + "guard is left to the rules that own those shapes.";

    /// <summary>The SST2264 rule description.</summary>
    private const string UseNamedEnumMemberDescription =
        "A numeric literal cast to an enum — '(RegexOptions)1' — hides which member it means behind its underlying "
        + "value, so the reader has to know the enum's numbering to read the code. Naming the member, "
        + "'RegexOptions.IgnoreCase', states the intent and survives a renumbering of the enum. Reported only when the "
        + "literal resolves to exactly one named member; a value that combines members or names none is left alone.";

    /// <summary>The SST2265 rule description.</summary>
    private const string FoldFluentCallChainDescription =
        "Two or more consecutive statements that each call a member on the same receiver and get that receiver "
        + "back — 'builder.Append(a); builder.Append(b);' — are one fluent chain written as separate statements. "
        + "Folding them into 'builder.Append(a).Append(b);' names the receiver once and reads as the single "
        + "operation it is. This is a house-style preference, so it ships disabled; reported only when the receiver "
        + "is a side-effect-free name and every call in the run returns that receiver's own type, so the chain binds "
        + "the same way and the receiver is evaluated once.";

    /// <summary>The SST2266 rule description.</summary>
    private const string InlineSingleUseLocalDescription =
        "A local that is assigned once and read exactly once holds a name the reader must carry to a single "
        + "downstream use. Inlining the initializer into that use removes the hop. Reported only when the local has "
        + "one initializer and one read, the initializer has no side effect that inlining would move or duplicate, "
        + "and the local is neither captured, aliased by 'ref'/'out', nor referenced by name elsewhere. Off by "
        + "default because a well-named intermediate local is often kept deliberately for readability.";

    /// <summary>The SST2267 rule description.</summary>
    private const string NormalizeInfiniteLoopStyleDescription =
        "A loop meant to run forever is written two ways — 'for (;;)' and 'while (true)' — and a codebase that "
        + "mixes them makes the reader notice the difference where there is none. This rule normalizes to one form, "
        + "chosen by 'stylesharp.infinite_loop_style' ('while' by default, or 'for'). Off by default because it is "
        + "a pure preference; the two forms compile identically.";

    /// <summary>The SST2268 rule description.</summary>
    private const string NormalizeObjectCreationParenthesesDescription =
        "An object creation that carries an initializer can be written with or without the empty argument "
        + "parentheses — 'new T() { ... }' or 'new T { ... }'. This rule normalizes to one form, chosen by "
        + "'stylesharp.object_creation_parentheses' ('omit' by default, or 'include'). Only creations that already "
        + "have an initializer are considered; a creation with real constructor arguments is never touched. Off by "
        + "default because it is a pure preference.";

    /// <summary>The SST2269 rule description.</summary>
    private const string NormalizeConditionalConditionParenthesesDescription =
        "The condition of a conditional expression is sometimes wrapped in parentheses — '(ready) ? a : b' — and "
        + "sometimes not — 'ready ? a : b'. When the condition is a single simple token the parentheses add "
        + "nothing. This rule normalizes to one form, chosen by 'stylesharp.conditional_condition_parentheses' "
        + "('omit_when_single_token' by default, or 'include'). Off by default because it is a pure preference.";

    /// <summary>The SST2270 rule description.</summary>
    private const string NormalizeArrayCreationTypeStyleDescription =
        "An array creation with an initializer can name its element type — 'new string[] { a, b }' — or let the "
        + "compiler infer it — 'new[] { a, b }'. This rule normalizes to one form, chosen by "
        + "'stylesharp.array_creation_type_style' ('explicit', 'implicit', or 'implicit_when_obvious'). It governs "
        + "only the element-type axis and never suggests a collection expression. Reported only when the rewritten "
        + "form binds to the same array type, which the fix confirms before it is offered. Off by default because "
        + "it is a pure preference.";

    /// <summary>The SST2271 rule description.</summary>
    private const string NormalizeVarStyleDescription =
        "A local declaration and a 'foreach' variable can name their type explicitly or use 'var'. This rule "
        + "normalizes to one choice, set by 'stylesharp.use_var' ('always', 'never', or 'when_obvious', where "
        + "'obvious' means the type is already named on the right-hand side by a 'new', a cast, or a literal). "
        + "Reported only when the type can be resolved and the rewritten declaration binds to the same type, which "
        + "the fix confirms. Off by default because whether 'var' or the type reads better is a house-style call.";

    /// <summary>The SST2272 rule description.</summary>
    private const string NormalizeEnumFlagValueStyleDescription =
        "The single-bit members of a '[Flags]' enum can be written as decimal values — '4' — or as bit shifts — "
        + "'1 << 2'. A codebase that mixes them obscures which members are single flags. This rule normalizes the "
        + "single-bit members to one form, chosen by 'stylesharp.enum_flag_value_style' ('shift' by default, or "
        + "'decimal'). Only members whose constant value is a single set bit — a power of two, or zero — are "
        + "touched; a combined value such as '3' or 'Read | Write' is left alone. Off by default because it is a "
        + "pure preference.";

    /// <summary>The SST2273 rule description.</summary>
    private const string PreferGuardClauseDescription =
        "A method, constructor, accessor, local function, or 'for'/'foreach'/'while' loop whose real work is the "
        + "last thing it does — wrapped in a single trailing 'if (cond) { work }' with no 'else' — reads with one "
        + "less level of nesting as an early-exit guard: 'if (!cond) return;' (or 'continue;' inside a loop) "
        + "followed by the unwrapped work. The guard states the exit up front and lets the body run at the outer "
        + "level. This is a house-style preference, so it ships disabled; enable it in .editorconfig for a codebase "
        + "that writes its exits as guards. Reported only when the guard can actually be expressed — a void or "
        + "async-Task-returning member, a constructor, a value-free accessor, or a loop 'continue' — and never when "
        + "a value would have to be produced on the guard path. The wrapped work must hold at least "
        + "'stylesharp.SST2273.min_wrapped_statements' statements (2 by default), so a one-line 'if (x) Do();' is "
        + "left alone.";

    /// <summary>The SST2243 rule description.</summary>
    private const string UseRawStringLiteralDescription =
        "A verbatim string full of doubled-quote escapes, or one spanning multiple lines, reads with less escape noise as a raw string "
        + "literal while the content stays character-for-character identical. Reported only when the language version supports raw "
        + "strings (C# 11).";

    /// <summary>The UppercaseLiteralSuffix rule description.</summary>
    private const string UppercaseLiteralSuffixDescription =
        "A lower-case 'l' is very nearly a '1' in most fonts, so '1l' reads as eleven. The other suffixes follow the same rule for "
        + "consistency rather than because they are ambiguous.";

    /// <summary>The SST2251 rule description.</summary>
    private const string OmitInferableTypeArgumentsDescription =
        "A method call that names its type arguments explicitly repeats what the compiler infers from the call's arguments. "
        + "It is reported only when the call bound without the type-argument list resolves to the identical constructed method, "
        + "so the arguments are provably redundant and can be dropped without changing which method the call means.";

    /// <summary>The UseWhileOverFor rule description.</summary>
    private const string UseWhileOverForDescription =
        "'for (; condition; )' is a 'while' with two empty clauses the reader still has to check. Writing 'while (condition)' says the same "
        + "thing with nothing left over.";

    /// <summary>The ConvertChainedConditionalToSwitch rule description.</summary>
    private const string ConvertChainedConditionalToSwitchDescription =
        "A run of nested '?:' expressions that all compare the same value against constants — "
        + "'x == 1 ? a : x == 2 ? b : c' — is a switch expression wearing conditional syntax. "
        + "'x switch { 1 => a, 2 => b, _ => c }' names the value once and lines each case up against its result. "
        + "Reported only when the tested value is a side-effect-free local, parameter, or field of a type whose "
        + "'==' matches a constant pattern, the language version supports switch expressions (C# 8), and the "
        + "rewritten switch binds to the same type.";

    /// <summary>The DeconstructMemberCopies rule description.</summary>
    private const string DeconstructMemberCopiesDescription =
        "Consecutive locals that each copy one member of the same tuple or Deconstruct-able value ('var a = pair.Item1; var b = pair.Item2;') "
        + "are declared once with deconstruction ('var (a, b) = pair;'), keeping the parts beside the value they come from. Reported only when "
        + "the reads cover every position of the value in order and the value is a side-effect-free local or parameter (C# 7).";

    /// <summary>The UseComparisonPattern rule description.</summary>
    private const string UseComparisonPatternDescription =
        "Two comparisons of the same side-effect-free value against constants read as one pattern: a range such as 'x >= 0 && x <= 9' "
        + "becomes 'x is >= 0 and <= 9', a set such as 't == A || t == B' becomes 't is A or B', and the region outside a range such as "
        + "'n < 0 || n > 100' becomes 'n is < 0 or > 100'. Reported from C# 9, only when the subject is a local, field, or parameter of an "
        + "integral, char, or enum type, and only for combinations whose merged pattern matches the original result exactly.";

    /// <summary>The UseInterpolatedString rule description.</summary>
    private const string UseInterpolatedStringDescription =
        "A composite format call such as Format(\"{0} of {1}\", part, whole), or a chain of '+' that splices values between literals, "
        + "makes the reader assemble the result in their head from a numbered template and a trailing argument list, or track a value "
        + "through the concatenation. An interpolated string puts each value where it lands in the text. Reported only when the "
        + "language version supports interpolation (C# 6) and the rewrite is provably identical, which the fix confirms before it is "
        + "offered. A composite format that passes an explicit format provider is left alone, because a plain interpolated string uses "
        + "the current culture and would silently drop the culture the call chose.";

    /// <summary>The JoinDeclarationAndAssignment rule description.</summary>
    private const string JoinDeclarationAndAssignmentDescription =
        "A local declared without a value and then given one by the very next statement holds the reader across a gap that carries no "
        + "meaning. Putting the value on the declaration keeps the name and its first value together. Reported only when the first "
        + "assignment is the immediately following straight-line statement at the same block level, so nothing reads the local in "
        + "between and joining cannot move evaluation or change definite assignment.";

    /// <summary>The SST2252 rule description.</summary>
    private const string AvoidNestedSwitchStatementDescription =
        "A switch statement inside another switch statement's section stacks two multi-way branches at one point, so the reader "
        + "tracks two governing values and both sets of cases at once. Lifting the inner switch into its own method, a switch "
        + "expression, or a lookup keeps each branch readable on its own. Only a switch statement nested in another switch "
        + "statement's section is reported; a switch expression is the preferred compact form and is left alone, and a switch "
        + "inside a lambda or local function declared in a section belongs to that body rather than to the enclosing switch.";

    /// <summary>The SST2254 rule description.</summary>
    private const string UseExplicitObjectCreationTypeDescription =
        "A target-typed 'new(...)' leaves the constructed type to be recovered from the target of the expression — the "
        + "declared variable, the field or property, the return type, the parameter. Writing the type at the creation site, "
        + "'new Type(...)', states what is being built where it is built, at the cost of repeating the type name. This is a "
        + "minority preference and the inverse of the default target-typed-'new' suggestion, so it ships disabled; enable it "
        + "in .editorconfig for a codebase that wants every object creation to name its type. Reported only when the created "
        + "type resolves to a name that can be written in source and the explicit form provably constructs the same type, "
        + "which the fix confirms before it is offered.";

    /// <summary>The SST2274 rule description.</summary>
    private const string ConvertAsAssignmentToIsPatternDescription =
        "A local assigned from an 'as' conversion and then tested for null — 'var s = o as T; if (s != null) "
        + "{ ...uses of s... }' — splits one type test across a declaration and a separate null check. An 'is' "
        + "declaration pattern, 'if (o is T s) { ...uses of s... }', states the test and the binding in one place. "
        + "The mirror early-exit shape 'var s = o as T; if (s == null) return;' becomes 'if (o is not T s) return;', "
        + "and the later uses of 's' are left unchanged. Reported only when the conversion targets a reference type "
        + "(a nullable value type such as 'o as int?' is left alone because 'o is int s' would bind a non-null value "
        + "of a different type), the local has a single declarator and is never reassigned, the null check is the "
        + "statement immediately after the declaration, the 'as' operand is side-effect-free so it can be re-read at "
        + "the test, and every use of the local still resolves against the pattern variable — which the fix confirms "
        + "before it is offered.";

    /// <summary>The SST2275 rule description.</summary>
    private const string UseExpressionBodyForMethodDescription =
        "A method whose block body does nothing but return one expression — 'int Area() { return width * height; }' — "
        + "or run one call — 'void Log() { _sink.Write(line); }' — wraps that single expression in a pair of braces "
        + "and, for the returning form, a 'return' keyword. The expression-bodied form 'int Area() => width * height;' "
        + "says the same thing with none of the ceremony. The rewrite is pure syntax and never changes what the method "
        + "computes, and it also settles any single-line-block warning by removing the block. Enabled at Info as a "
        + "gentle nudge.";

    /// <summary>The SST2276 rule description.</summary>
    private const string UseExpressionBodyForConstructorDescription =
        "A constructor whose block body is a single call — 'public C(int x) { Configure(x); }' — can be written with an "
        + "expression body 'public C(int x) => Configure(x);'. Whether a constructor reads better on one line is more "
        + "contested than for a plain method, so the rule ships disabled; enable it in .editorconfig for a codebase "
        + "that prefers it. Reported only when the constructor has no ': this(...)' or ': base(...)' initializer, so "
        + "nothing is lost in the rewrite.";

    /// <summary>The SST2277 rule description.</summary>
    private const string UseExpressionBodyForOperatorDescription =
        "An operator whose block body is a single 'return expr;' — 'public static V operator +(V a, V b) { return "
        + "new V(a.X + b.X); }' — can use an expression body '... => new V(a.X + b.X);'. Whether operators read better "
        + "on one line is a house-style call, so the rule ships disabled. The rewrite is pure syntax and never changes "
        + "what the operator computes.";

    /// <summary>The SST2278 rule description.</summary>
    private const string UseExpressionBodyForConversionOperatorDescription =
        "A conversion operator whose block body is a single 'return expr;' — 'public static implicit operator int(V v) "
        + "{ return v.X; }' — can use an expression body '... => v.X;'. Like the arithmetic-operator rule it ships "
        + "disabled, because whether a conversion reads better on one line is a preference. The rewrite is pure syntax "
        + "and never changes the conversion.";

    /// <summary>The SST2279 rule description.</summary>
    private const string UseExpressionBodyForPropertyDescription =
        "A property with a single 'get' accessor whose block body is one 'return expr;' — 'public int Count { get "
        + "{ return _items.Length; } }' — can be written as a whole-member expression body 'public int Count => "
        + "_items.Length;'. Reported only for a get-only property: a property with a setter, an 'init' accessor, or "
        + "more than one accessor keeps its per-accessor shape. An accessor carrying its own attributes or modifiers "
        + "is left alone, because the whole-member form cannot hold them. Converting also settles any single-line-block "
        + "warning on the accessor.";

    /// <summary>The SST2280 rule description.</summary>
    private const string UseExpressionBodyForIndexerDescription =
        "An indexer with a single 'get' accessor whose block body is one 'return expr;' — 'public T this[int i] { get "
        + "{ return _items[i]; } }' — can be written as a whole-member expression body 'public T this[int i] => "
        + "_items[i];'. Reported only for a get-only indexer; a setter, an 'init' accessor, or an accessor with its "
        + "own attributes or modifiers keeps the block form. Converting also settles any single-line-block warning on "
        + "the accessor.";

    /// <summary>The SST2281 rule description.</summary>
    private const string UseExpressionBodyForLocalFunctionDescription =
        "A local function whose block body does nothing but return one expression, or run one call, can use an "
        + "expression body — 'int Double(int n) { return n * 2; }' becomes 'int Double(int n) => n * 2;'. The rewrite "
        + "is pure syntax and never changes what the local function computes, and it also settles any single-line-block "
        + "warning by removing the block. Enabled at Info as a gentle nudge.";

    /// <summary>The SST2282 rule description.</summary>
    private const string UseNullPatternOverReferenceEqualsDescription =
        "'object.ReferenceEquals(value, null)' — in either argument order — is a reference comparison against null "
        + "written as a static call. 'value is null' states the same check as a pattern that reads left to right and "
        + "needs no receiver; the negated '!ReferenceEquals(value, null)' becomes 'value is not null'. Reported only "
        + "when the call binds to 'System.Object.ReferenceEquals' and the non-null operand is a reference type or an "
        + "unconstrained type parameter, where 'is null' is legal. A value-type operand is left alone: 'ReferenceEquals' "
        + "boxes it, so rewriting the shape would change what runs — a separate concern. The negated form is reported "
        + "only where the language supports the 'is not null' pattern (C# 9); the plain form needs the constant null "
        + "pattern (C# 7).";

    /// <summary>The SST2283 rule description.</summary>
    private const string FoldGuardIntoAssignedValueDescription =
        "A null guard whose one statement throws, immediately followed by assigning the guarded value to a field, "
        + "property, or local — 'if (x is null) throw new SomeException(); _x = x;' — holds the guard and the value "
        + "apart. Folding the throw into the assignment, '_x = x ?? throw new SomeException();', states the contract in "
        + "one place and evaluates the value once. Reported only when the guarded value is a side-effect-free local or "
        + "parameter of a reference type — so the coalescing throw is legal and identical — the throw carries an "
        + "expression, the assignment target is a simple name or a 'this' member with no receiver of its own, and "
        + "nothing sits between the guard and the assignment. The guard-then-return shape, the assign-then-check shape, "
        + "and the argument-null guard whose throw is better expressed as a runtime null-check helper are each left to "
        + "the rule that owns them, so this rule never fires on a guard another already covers.";

    /// <summary>Creates a Warning-severity ModernSyntax descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "ModernSyntax", description);

    /// <summary>
    /// Creates an enabled-by-default Info-severity ModernSyntax descriptor — a modernization nudge where the code
    /// still compiles and runs correctly, so a build-breaking Warning would be too strong.
    /// </summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateInfo(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "ModernSyntax",
            DiagnosticSeverity.Info,
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
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "ModernSyntax", description);
}
