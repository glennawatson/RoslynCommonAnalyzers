// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the maintainability (SST14xx) diagnostic descriptors.
/// </summary>
internal static class MaintainabilityRules
{
    /// <summary>SST1400 — an element does not declare an access modifier.</summary>
    public static readonly DiagnosticDescriptor AccessModifierDeclared = Create(
        "SST1400",
        "Access modifier should be declared",
        "'{0}' should declare an explicit access modifier",
        "Types and members declare their accessibility explicitly rather than relying on the implicit default.");

    /// <summary>SST1401 — a non-private, non-constant field is exposed.</summary>
    public static readonly DiagnosticDescriptor FieldsPrivate = Create(
        "SST1401",
        "Fields should be private",
        "Field '{0}' should be private; expose it through a property instead",
        "Fields are an implementation detail and should be private (constants may be any accessibility).");

    /// <summary>SST1402 — a file declares more than one top-level type.</summary>
    public static readonly DiagnosticDescriptor SingleType = Create(
        "SST1402",
        "File should contain a single type",
        "'{0}' should be moved to its own file; a file should declare a single top-level type",
        "Each file declares a single top-level type so types are easy to locate.");

    /// <summary>SST1403 — a file declares more than one namespace.</summary>
    public static readonly DiagnosticDescriptor SingleNamespace = Create(
        "SST1403",
        "File should contain a single namespace",
        "Namespace '{0}' should be the only namespace in the file",
        "Each file declares a single namespace.");

    /// <summary>SST1404 — a code-analysis suppression has no justification.</summary>
    public static readonly DiagnosticDescriptor SuppressionJustified = Create(
        "SST1404",
        "Code analysis suppression should have justification",
        "The suppression should set a non-empty 'Justification'",
        "Every [SuppressMessage] explains why the rule is suppressed.");

    /// <summary>SST1405 — a <c>Debug.Assert</c> call provides no message.</summary>
    public static readonly DiagnosticDescriptor AssertMessage = Create(
        "SST1405",
        "Debug.Assert should provide message text",
        "The Debug.Assert call should provide a message describing the assertion",
        "Debug.Assert calls describe the failed assumption for whoever hits them.");

    /// <summary>SST1406 — a <c>Debug.Fail</c> call provides no message.</summary>
    public static readonly DiagnosticDescriptor FailMessage = Create(
        "SST1406",
        "Debug.Fail should provide message text",
        "The Debug.Fail call should provide a message describing the failure",
        "Debug.Fail calls describe the failure for whoever hits them.");

    /// <summary>SST1407 — mixed-precedence arithmetic is not parenthesized.</summary>
    public static readonly DiagnosticDescriptor ArithmeticPrecedence = Create(
        "SST1407",
        "Arithmetic expressions should declare precedence",
        "Add parentheses to make the arithmetic precedence explicit",
        "Mixed arithmetic and shift operators are parenthesized so the intended precedence is clear.");

    /// <summary>SST1408 — mixed conditional operators are not parenthesized.</summary>
    public static readonly DiagnosticDescriptor ConditionalPrecedence = Create(
        "SST1408",
        "Conditional expressions should declare precedence",
        "Add parentheses to make the conditional precedence explicit",
        "Expressions mixing '&&' and '||' are parenthesized so the intended precedence is clear.");

    /// <summary>SST1410 — an anonymous method has an empty parameter list.</summary>
    public static readonly DiagnosticDescriptor RemoveDelegateParentheses = Create(
        "SST1410",
        "Remove delegate parenthesis when possible",
        "Remove the empty parameter list from the anonymous method",
        "An anonymous method with no parameters omits the empty parameter list.");

    /// <summary>SST1411 — an attribute uses an empty argument list.</summary>
    public static readonly DiagnosticDescriptor RemoveAttributeParentheses = Create(
        "SST1411",
        "Attribute constructor should not use unnecessary parenthesis",
        "Remove the empty argument list from the attribute",
        "An attribute with no arguments omits the empty parentheses.");

    /// <summary>SST1413 — a multi-line initializer omits the trailing comma.</summary>
    public static readonly DiagnosticDescriptor TrailingComma = Create(
        "SST1413",
        "Use a trailing comma in multi-line initializers",
        "Add a trailing comma after the last element",
        "The last element of a multi-line initializer or enum is followed by a trailing comma so reordering is clean.");

    /// <summary>SST1412 — files should be stored as UTF-8 with a byte order mark (opt-in; mutually exclusive with SST1450).</summary>
    public static readonly DiagnosticDescriptor Utf8WithBom = CreateOptIn(
        "SST1412",
        "Store files as UTF-8 with a byte order mark",
        "This file should be saved as UTF-8 with a byte order mark",
        "Source files are stored as UTF-8 with a byte order mark. Off by default — enable either this or SST1450, not both.");

    /// <summary>SST1450 — files should be stored as UTF-8 without a byte order mark (StyleSharp original; opt-in; mutually exclusive with SST1412).</summary>
    public static readonly DiagnosticDescriptor Utf8WithoutBom = CreateOptIn(
        "SST1450",
        "Store files as UTF-8 without a byte order mark",
        "This file should be saved as UTF-8 without a byte order mark",
        "Source files are stored as UTF-8 without a byte order mark. Off by default — enable either this or SST1412, not both.");

    /// <summary>SST1414 — a tuple type in a member signature has an unnamed element.</summary>
    public static readonly DiagnosticDescriptor TupleSignatureElementNames = Create(
        "SST1414",
        "Tuple types in signatures should have element names",
        "Name the elements of this tuple type",
        "A tuple type that appears in a member signature names each element so callers do not depend on positional 'ItemN' access.");

    /// <summary>SST1416 — a public member is declared in a type that is not externally visible (StyleSharp original; opt-in).</summary>
    public static readonly DiagnosticDescriptor NoPublicOnInternalType = CreateOptIn(
        "SST1416",
        "Do not declare public members in a non-public type",
        "Member '{0}' is public but its type is not externally visible; declare it internal",
        "A public member of a type that is not externally visible is misleading — its effective accessibility is limited by the type. Off by default — public-on-internal is a common habit.");

    /// <summary>SST1418 — a binary expression is an operand of <c>??</c> without parentheses (StyleSharp original; extends SST1407/SST1408).</summary>
    public static readonly DiagnosticDescriptor NullCoalescingPrecedence = Create(
        "SST1418",
        "Declare precedence when mixing the null-coalescing operator",
        "Parenthesize this expression to make its precedence with '??' explicit",
        "A binary expression used as an operand of the '??' operator is parenthesized so the precedence is explicit.");

    /// <summary>SST1417 — a namespace does not match the file's folder structure (StyleSharp original; opt-in).</summary>
    public static readonly DiagnosticDescriptor NamespaceMatchesFolder = CreateOptIn(
        "SST1417",
        "Namespace should match the folder structure",
        "Namespace '{0}' does not match the folder structure; expected '{1}'",
        "A type's namespace mirrors the file's folder path under the project root. Off by default — set 'stylesharp.namespace_root' in .editorconfig to override the root namespace.");

    /// <summary>SST1415 — an argument-exception constructor uses a string literal where <c>nameof</c> would track renames (StyleSharp original).</summary>
    public static readonly DiagnosticDescriptor UseNameofForParameter = Create(
        "SST1415",
        "Use nameof for parameter references",
        "Use 'nameof({0})' instead of the string literal \"{0}\"",
        "A string literal that names a parameter in an argument-exception constructor is written with 'nameof' so it follows renames.");

    /// <summary>SST1421 — a property has a setter but no getter.</summary>
    public static readonly DiagnosticDescriptor NoWriteOnlyProperty = Create(
        "SST1421",
        "Write-only properties should not be used",
        "Add a getter or replace this write-only property with a method",
        "A property exposes a readable value; write-only operations are represented by methods.");

    /// <summary>SST1423 — a switch statement exceeds the configured section count.</summary>
    public static readonly DiagnosticDescriptor TooManySwitchSections = Create(
        "SST1423",
        "Switch statements should not have too many sections",
        "Reduce this switch statement from {0} sections to at most {1}",
        "Large switch statements are split into smaller abstractions. The maximum section count is configurable and defaults to 30.");

    /// <summary>SST1419 — a modifier or overflow-check context has no effect in its context.</summary>
    public static readonly DiagnosticDescriptor NoRedundantModifier = Create(
        "SST1419",
        "Remove redundant modifiers",
        "Remove the redundant '{0}'",
        "A modifier the declaration context already guarantees, or a 'checked'/'unchecked' context with no operation whose result it could change, has no effect and is removed.");

    /// <summary>SST1420 — a property trivially wraps a private backing field.</summary>
    public static readonly DiagnosticDescriptor PreferAutoProperty = Create(
        "SST1420",
        "Trivial properties should be auto-implemented",
        "Convert this property to an auto-property and remove its backing field",
        "A property whose accessors only read and write a private single-use field is auto-implemented.");

    /// <summary>SST1422 — a private field acts only as method-local temporary storage (opt-in).</summary>
    public static readonly DiagnosticDescriptor PrivateFieldUsedAsLocal = CreateOptIn(
        "SST1422",
        "Private fields used only as locals should be local variables",
        "Move field '{0}' into the method that uses it",
        "A field that is reset before every use and referenced by one method does not represent object state. Off by default because conversion is a refactoring.");

    /// <summary>SST1424 — a private field is never assigned outside construction (opt-in).</summary>
    public static readonly DiagnosticDescriptor FieldShouldBeReadonly = CreateOptIn(
        "SST1424",
        "Fields that are never reassigned should be readonly",
        "Make field '{0}' readonly",
        "A private instance field assigned only by its initializer or constructors is declared readonly. Off by default because readonly conversion can be a source-compatibility decision.");

    /// <summary>SST1425 — a class/struct primary-constructor parameter is reassigned after capture.</summary>
    public static readonly DiagnosticDescriptor NoReassignedPrimaryConstructorParameter = Create(
        "SST1425",
        "Do not reassign captured primary-constructor parameters",
        "Do not reassign the captured primary-constructor parameter '{0}'",
        "Reassigning a class or struct primary-constructor parameter mutates its hidden captured field, which is surprising. Use a separate local or explicit field instead.");

    /// <summary>SST1426 — a <c>#pragma warning disable</c> silences an analyzer warning that a scoped <c>[SuppressMessage]</c> should handle (StyleSharp original).</summary>
    public static readonly DiagnosticDescriptor PreferSuppressMessageOverPragma = Create(
        "SST1426",
        "Use [SuppressMessage] instead of #pragma warning disable",
        "Suppress '{0}' with a scoped [SuppressMessage] attribute instead of '#pragma warning disable'",
        "Analyzer warnings use a scoped [SuppressMessage] attribute, not a #pragma warning disable directive. Compiler (CS) warnings are exempt because only a pragma can disable them.");

    /// <summary>SST1427 — a <c>protected</c> member of a sealed type has no effect, since the type cannot be derived.</summary>
    public static readonly DiagnosticDescriptor NoProtectedInSealed = Create(
        "SST1427",
        "Protected members of sealed types should not be used",
        "Make this member private; 'protected' has no effect in a sealed type",
        "A sealed type cannot be derived from, so a 'protected' member is reachable only as if it were private. Mark it private (or drop 'protected' from 'protected internal').");

    /// <summary>SST1428 — an abstract type exposes a <c>public</c> constructor that only derived types can call.</summary>
    public static readonly DiagnosticDescriptor NoPublicConstructorOnAbstractType = Create(
        "SST1428",
        "Abstract types should not declare public constructors",
        "Make this constructor 'protected'; only a derived type can call it",
        "An abstract type cannot be instantiated directly, so a 'public' constructor is misleading. A 'protected' (or private) constructor states that only derived types call it.");

    /// <summary>SST1429 — a <c>catch</c> clause swallows the base <see cref="Exception"/> with an empty body.</summary>
    public static readonly DiagnosticDescriptor NoEmptyCatchOfBaseException = Create(
        "SST1429",
        "Empty catch clauses should not swallow the base exception",
        "Handle, rethrow, or narrow this catch; an empty catch of the base exception hides failures",
        "A 'catch (Exception) { }' (or bare 'catch { }') with an empty body silently discards every error, including ones the code is not prepared for. Handle it, rethrow, or catch a narrower type.");

    /// <summary>SST1430 — <c>throw ex;</c> in a catch resets the captured stack trace instead of preserving it.</summary>
    public static readonly DiagnosticDescriptor PreserveStackTraceOnRethrow = Create(
        "SST1430",
        "Rethrow with 'throw;' to preserve the stack trace",
        "Use 'throw;' instead of 'throw {0};' so the original stack trace is kept",
        "Re-throwing the caught exception with 'throw ex;' overwrites its stack trace with the rethrow location. A bare 'throw;' keeps the original trace intact.");

    /// <summary>SST1431 — a <c>static</c> member of a generic type never mentions the type's type parameters.</summary>
    public static readonly DiagnosticDescriptor StaticMemberShouldUseTypeParameter = Create(
        "SST1431",
        "Static members of a generic type should use a type parameter",
        "Reference a type parameter, or move '{0}' off the generic type",
        "A static member whose signature ignores its generic type's type parameters forces callers to pick an arbitrary type argument, and usually belongs on a non-generic type.");

    /// <summary>SST1432 — a class declares only static members yet is not marked <c>static</c> (opt-in).</summary>
    public static readonly DiagnosticDescriptor MakeClassStatic = CreateOptIn(
        "SST1432",
        "Classes with only static members should be static",
        "Mark '{0}' as 'static'; it declares only static members",
        "A class whose members are all static can be marked 'static' to forbid instantiation. Off by default, since marking a published, instantiable type 'static' is a breaking change.");

    /// <summary>SST1433 — a public parameterless constructor with an empty body restates the compiler default.</summary>
    public static readonly DiagnosticDescriptor NoRedundantConstructor = Create(
        "SST1433",
        "Redundant constructors should be removed",
        "Remove this redundant constructor; the compiler supplies an equivalent default constructor",
        "A type's only constructor being a public, parameterless, empty constructor restates the default constructor the compiler would emit anyway, so it can be removed.");

    /// <summary>SST1435 — a namespace declaration contains no members.</summary>
    public static readonly DiagnosticDescriptor NoEmptyNamespace = Create(
        "SST1435",
        "Empty namespace declarations should be removed",
        "Remove this empty namespace declaration",
        "A namespace declaration with no members serves no purpose and can be deleted.");

    /// <summary>SST1436 — a class, struct, or record declares no members (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyType = CreateOptIn(
        "SST1436",
        "Empty types should not be declared",
        "Add members to '{0}' or remove it; an empty type is rarely intentional",
        "A class, struct, or record with no members is usually an oversight. Off by default, because empty types are sometimes used as markers, DTOs, or extension points.");

    /// <summary>SST1437 — an interface declares no members (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyInterface = CreateOptIn(
        "SST1437",
        "Empty interfaces should not be declared",
        "Add members to '{0}' or remove it; an empty interface adds no contract",
        "An interface with no members defines no contract and is often better expressed with an attribute. Off by default, because marker interfaces are an accepted pattern.");

    /// <summary>SST1438 — a method has an empty body (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyMethod = CreateOptIn(
        "SST1438",
        "Methods should not be empty",
        "Give '{0}' a body or document why it is intentionally empty",
        "An empty method body usually signals unfinished work. Off by default, because empty overrides, virtual hooks, and interface implementations are legitimately empty.");

    /// <summary>SST1439 — a loop or guard statement has an empty embedded block.</summary>
    public static readonly DiagnosticDescriptor NoEmptyNestedBlock = Create(
        "SST1439",
        "Nested code blocks should not be left empty",
        "Fill or remove this empty block; an empty loop or guard body is usually a mistake",
        "An empty block as the body of a loop ('while', 'for', 'foreach', 'do') or guard ('if', 'lock', 'fixed', 'using') usually means missing code or a stray semicolon.");

    /// <summary>SST1440 — a private member has no reads or calls in its declaring type.</summary>
    public static readonly DiagnosticDescriptor RemoveUnusedPrivateMember = Create(
        "SST1440",
        "Remove private members with no local use",
        "Remove private member '{0}' because it has no local use",
        "A private member that is never read, called, or otherwise referenced in its declaring type adds dead surface area and can be removed.");

    /// <summary>SST1441 — a private field is assigned but never read.</summary>
    public static readonly DiagnosticDescriptor RemoveUnreadPrivateField = Create(
        "SST1441",
        "Remove private fields that are never read",
        "Remove private field '{0}' or its dead writes because the value is never read",
        "A private field that receives values but is never read does not represent observable state. Remove the field or the dead writes that feed it.");

    /// <summary>SST1442 - a function's branching complexity exceeds the configured maximum.</summary>
    public static readonly DiagnosticDescriptor CyclomaticComplexity = Create(
        "SST1442",
        "Functions should keep branching complexity low",
        "Reduce the branching complexity of this {2} from {1} to at most {0}",
        "Functions keep their direct decision count below the configured threshold. Wide switch dispatch counts once so table-like code is not penalized.");

    /// <summary>SST1443 - a function's nested-flow complexity exceeds the configured maximum.</summary>
    public static readonly DiagnosticDescriptor CognitiveComplexity = Create(
        "SST1443",
        "Functions should keep nested flow easy to follow",
        "Reduce the nested-flow complexity of this {0} from {1} to at most {2}",
        "Functions keep nesting-heavy control flow below the configured threshold. Flat dispatch is cheaper than nested branching.");

    /// <summary>SST1444 - a loop always exits or continues before a second iteration can run.</summary>
    public static readonly DiagnosticDescriptor SingleIterationLoop = Create(
        "SST1444",
        "Loops should do more than one iteration",
        "Replace this loop jump or restructure the loop so another iteration can run",
        "A loop whose body unconditionally jumps before another iteration can run is usually clearer as straight-line code or a conditional.");

    /// <summary>SST1445 - a using directive imports a namespace, type, or alias the file never uses.</summary>
    public static readonly DiagnosticDescriptor UnnecessaryUsingDirective = Create(
        "SST1445",
        "Remove unnecessary using directives",
        "The using directive for '{0}' is unnecessary",
        "A using directive nothing in the file resolves through is dead weight: it slows reading, invites name collisions, and hides which dependencies the file really has.");

    /// <summary>SST1446 - a class sits deeper in an inheritance chain than the configured maximum.</summary>
    public static readonly DiagnosticDescriptor InheritanceDepth = Create(
        "SST1446",
        "Inheritance chains should stay shallow",
        "'{0}' is {1} levels deep in its inheritance chain, which exceeds the maximum of {2}",
        "Every inheritance level adds state and behavior a reader must load to understand the leaf type; deep chains are rigid and hard to change. Prefer composition or flatter hierarchies.");

    /// <summary>SST1447 - an Equals or GetHashCode override delegates to object's reference-based implementation.</summary>
    public static readonly DiagnosticDescriptor BaseObjectEqualityDelegation = Create(
        "SST1447",
        "Equality overrides should not delegate to object",
        "'{0}' calls the object implementation of '{1}', which compares by reference identity instead of the value semantics this override implies",
        "Calling base.Equals or base.GetHashCode when the base class is object silently reintroduces reference semantics inside an override whose purpose is value semantics.");

    /// <summary>SST1448 - an argument is passed explicitly to a caller-info parameter.</summary>
    public static readonly DiagnosticDescriptor CallerInfoArgument = Create(
        "SST1448",
        "Let the compiler supply caller-info arguments",
        "Remove this argument so the compiler supplies the caller's {0}",
        "Caller-info parameters exist so the compiler injects the calling member, file, or line; passing a value explicitly defeats that and usually reports the wrong call site.");

    /// <summary>SST1449 - code writes directly to the console.</summary>
    public static readonly DiagnosticDescriptor NoConsoleOutput = Create(
        "SST1449",
        "Avoid writing directly to the console",
        "Replace this '{0}' call with the application's logging abstraction",
        "Direct console writes bypass log levels, sinks, and redirection, and turn into noise or lost output when no console is attached. Route diagnostics through a logger.");

    /// <summary>SST1451 - a DateTime is created without specifying its kind.</summary>
    public static readonly DiagnosticDescriptor DateTimeKindRequired = Create(
        "SST1451",
        "Specify a DateTimeKind when creating a DateTime",
        "Specify a DateTimeKind so consumers know whether this DateTime is UTC or local",
        "A DateTime constructed without a kind is DateTimeKind.Unspecified and conversions silently guess; stating the kind (or using DateTimeOffset) makes the timeline explicit.");

    /// <summary>SST1452 - a declared generic type parameter is never used.</summary>
    public static readonly DiagnosticDescriptor UnusedTypeParameter = Create(
        "SST1452",
        "Remove unused type parameters",
        "The type parameter '{0}' is never used; remove it",
        "A type parameter nothing references forces every caller to supply a meaningless type argument and usually marks a refactoring leftover.");

    /// <summary>SST1453 - code appears after a statement that unconditionally leaves the block.</summary>
    public static readonly DiagnosticDescriptor NoUnreachableCode = Create(
        "SST1453",
        "Unreachable code should be removed",
        "Remove this unreachable statement",
        "Statements after an unconditional return, throw, break, or continue in the same block never execute and should be removed.");

    /// <summary>SST1454 - a composite format string contains a placeholder that cannot be satisfied.</summary>
    public static readonly DiagnosticDescriptor ValidCompositeFormatString = Create(
        "SST1454",
        "Composite format strings should match their arguments",
        "Fix this composite format string; one of its placeholders cannot be satisfied",
        "A composite format string is checked against the supplied arguments so invalid or out-of-range placeholders are caught at compile time.");

    /// <summary>SST1455 - an unsafe modifier is present where the declaration contains no unsafe construct.</summary>
    public static readonly DiagnosticDescriptor NoUnnecessaryUnsafeModifier = Create(
        "SST1455",
        "Remove unnecessary unsafe modifiers",
        "Remove the unnecessary 'unsafe' modifier",
        "An unsafe modifier is used only where the declaration actually contains pointer, function-pointer, fixed, or sizeof syntax that requires unsafe context.");

    /// <summary>SST1456 - a readonly field stores a mutable source-defined struct.</summary>
    public static readonly DiagnosticDescriptor NoReadonlyMutableStructField = Create(
        "SST1456",
        "Mutable struct fields should not be readonly",
        "Remove 'readonly' from field '{0}', or make the struct readonly",
        "A readonly field of a mutable struct still allows defensive-copy surprises on member access. Make the struct immutable or keep the field writable so the mutability is explicit.");

    /// <summary>SST1457 - a global suppression target cannot be resolved to a declaration in the compilation.</summary>
    public static readonly DiagnosticDescriptor ValidGlobalSuppressionTarget = Create(
        "SST1457",
        "Global suppressions should point at real declarations",
        "Fix or remove this global suppression target; '{0}' does not resolve in this compilation",
        "A global SuppressMessage target should resolve to the declaration it suppresses so stale suppression entries are removed instead of silently accumulating.");

    /// <summary>SST1458 - a global suppression target uses the old tilde-prefixed target spelling.</summary>
    public static readonly DiagnosticDescriptor UseDeclarationIdSuppressionTarget = Create(
        "SST1458",
        "Global suppression targets should use declaration ids directly",
        "Remove the legacy '~' prefix from this global suppression target",
        "Global SuppressMessage targets use declaration ids directly, which keeps the string consumable by Roslyn's documentation-id resolver without an extra legacy marker.");

    /// <summary>SST1459 - parentheses wrap an expression in a context where grouping has no effect.</summary>
    public static readonly DiagnosticDescriptor RemoveUnnecessaryParentheses = Create(
        "SST1459",
        "Remove grouping parentheses that do not group anything",
        "Remove these parentheses; this expression is already isolated by its containing syntax",
        "Parentheses around a standalone return value, argument, initializer, or assignment value add visual noise when no operator precedence is being clarified.");

    /// <summary>SST1460 - a struct instance member can be readonly because its body does not mutate state.</summary>
    public static readonly DiagnosticDescriptor MakeStructMemberReadonly = Create(
        "SST1460",
        "Mark non-mutating struct members readonly",
        "Mark '{0}' as readonly so it can be called without a defensive copy",
        "A struct member that only reads state is marked readonly so calls through readonly receivers avoid defensive-copy overhead and communicate the member's non-mutating contract.");

    /// <summary>SST1461 - a private parameter is never read by its declaration body.</summary>
    public static readonly DiagnosticDescriptor RemoveUnusedPrivateParameter = Create(
        "SST1461",
        "Remove private parameters that are never read",
        "Remove parameter '{0}' because this private declaration never reads it",
        "A private or local-function parameter that is not read by its body adds dead API surface inside the type and usually marks an unfinished refactor.");

    /// <summary>SST1462 - a suppression targets a diagnostic that is disabled in the active analyzer config scope.</summary>
    public static readonly DiagnosticDescriptor RemoveDisabledDiagnosticSuppression = Create(
        "SST1462",
        "Remove suppressions for diagnostics that are already disabled",
        "Remove this suppression because diagnostic '{0}' is disabled in this analyzer config scope",
        "A SuppressMessage attribute for a diagnostic configured to 'none' or 'silent' cannot suppress a live report and should not stay in source as stale policy documentation.");

    /// <summary>SST1463 - a string literal names an in-scope symbol in a name-shaped argument.</summary>
    public static readonly DiagnosticDescriptor UseNameofForSymbolName = Create(
        "SST1463",
        "Use nameof for symbol-name strings",
        "Use 'nameof({0})' so this symbol-name string follows renames",
        "A string literal passed to a name-shaped parameter is written with nameof when it matches an in-scope symbol, keeping rename operations correct without reflection or runtime lookup.");

    /// <summary>SST1464 — an else clause follows a branch that never falls through.</summary>
    public static readonly DiagnosticDescriptor UnwrapElseAfterJump = Create(
        "SST1464",
        "Unwrap else after a branch that does not fall through",
        "Unwrap this 'else'; the preceding branch always jumps",
        "When the if branch ends in a return, throw, break, or continue, the else wrapper only adds nesting; moving its statements after the if reads straighter.");

    /// <summary>SST1465 — an else block contains only an if statement.</summary>
    public static readonly DiagnosticDescriptor CollapseElseIntoElseIf = Create(
        "SST1465",
        "Collapse an else block that only wraps an if",
        "Write this as 'else if'",
        "An else block whose only statement is an if statement flattens into an 'else if' chain, removing a level of nesting without changing the branch order.");

    /// <summary>SST1466 — a case label shares a switch section with the default label.</summary>
    public static readonly DiagnosticDescriptor RemoveCaseBesideDefault = Create(
        "SST1466",
        "Remove case labels that share a section with default",
        "Remove this case label; the default label already routes here",
        "A case label in the same switch section as the default label selects the body the default already provides; it stays only when a 'goto case' statement targets it.");

    /// <summary>SST1467 — a loop drives an enumerator by hand instead of using foreach.</summary>
    public static readonly DiagnosticDescriptor UseForeachOverManualEnumerator = Create(
        "SST1467",
        "Enumerate with foreach instead of driving the enumerator by hand",
        "Replace this manual enumerator loop with 'foreach'",
        "A while loop over GetEnumerator, MoveNext, and Current restates what foreach compiles to, without the guaranteed disposal and scoping that foreach provides.");

    /// <summary>SST1468 — boolean logic uses a non-short-circuiting operator.</summary>
    public static readonly DiagnosticDescriptor UseShortCircuitOperator = Create(
        "SST1468",
        "Boolean logic should short-circuit",
        "Use '{0}' so the right operand is evaluated only when it can matter",
        UseShortCircuitOperatorDescription);

    /// <summary>SST1469 — a non-nullable value type is compared to null.</summary>
    public static readonly DiagnosticDescriptor ValueTypeNullComparison = Create(
        "SST1469",
        "Do not compare a value type to null",
        "'{0}' is a non-nullable value type; this comparison is always {1}",
        "A non-nullable value type can never be null, so the comparison folds to a constant and usually points at a misread type or a missing '?'.");

    /// <summary>SST1470 — a trailing catch clause only rethrows.</summary>
    public static readonly DiagnosticDescriptor RemoveRethrowOnlyCatch = Create(
        "SST1470",
        "Remove a catch clause that only rethrows",
        "Remove this catch clause; it only rethrows the exception",
        "A final catch clause whose body is exactly 'throw;' with no filter changes nothing about how the exception propagates and hides the clauses that do work.");

    /// <summary>SST1471 — a numeric literal in an expression carries unexplained meaning.</summary>
    public static readonly DiagnosticDescriptor MagicNumber = Create(
        "SST1471",
        "Magic numbers should be named constants",
        "Replace the magic number '{0}' with a named constant",
        MagicNumberDescription);

    /// <summary>SST1472 — a signature declares more parameters than the configured maximum.</summary>
    public static readonly DiagnosticDescriptor TooManyParameters = Create(
        "SST1472",
        "Signatures should not declare too many parameters",
        "'{0}' declares {1} parameters, which exceeds the maximum of {2}; group the related ones into a type",
        TooManyParametersDescription);

    /// <summary>SST1473 — a binary floating-point value is compared for exact equality.</summary>
    public static readonly DiagnosticDescriptor FloatingPointEquality = Create(
        "SST1473",
        "Floating-point values should not be compared for exact equality",
        "Comparing '{0}' values with '{1}' is unreliable; compare against a tolerance instead",
        FloatingPointEqualityDescription);

    /// <summary>SST1474 — both operands of a binary operator are the same expression.</summary>
    public static readonly DiagnosticDescriptor IdenticalOperands = Create(
        "SST1474",
        "Identical expressions should not appear on both sides of an operator",
        "Both sides of '{0}' are the same expression; this is a copy-paste mistake or the operation is pointless",
        IdenticalOperandsDescription);

    /// <summary>SST1475 — a condition in an if/else-if chain repeats an earlier one.</summary>
    public static readonly DiagnosticDescriptor DuplicateCondition = Create(
        "SST1475",
        "A condition should not be repeated",
        "This condition repeats the one on line {0}; {1}",
        DuplicateConditionDescription);

    /// <summary>SST1476 — every branch of a conditional has the same body.</summary>
    public static readonly DiagnosticDescriptor IdenticalBranches = Create(
        "SST1476",
        "Conditional branches should not have identical bodies",
        "{0} have identical bodies, so the condition decides nothing",
        IdenticalBranchesDescription);

    /// <summary>SST1477 — an integer division is consumed as a floating-point value.</summary>
    public static readonly DiagnosticDescriptor IntegerDivisionAsFloatingPoint = Create(
        "SST1477",
        "Integer division should not be assigned to a floating-point target",
        "This division truncates before it is widened to '{0}'; cast an operand first to divide in floating point",
        IntegerDivisionAsFloatingPointDescription);

    /// <summary>SST1478 — a shift count is zero or at least the operand's width.</summary>
    public static readonly DiagnosticDescriptor SuspiciousShiftCount = Create(
        "SST1478",
        "Shift counts should be within the operand's width",
        "Shifting a {0}-bit value by {1} {2}",
        SuspiciousShiftCountDescription);

    /// <summary>SST1479 — a count or length is compared against a value it can never take.</summary>
    public static readonly DiagnosticDescriptor MeaninglessCountComparison = Create(
        "SST1479",
        "Count and length comparisons should be satisfiable",
        "A count is never negative, so this comparison is always {0}",
        MeaninglessCountComparisonDescription);

    /// <summary>SST1480 — an exception is constructed but never thrown.</summary>
    public static readonly DiagnosticDescriptor ExceptionNeverThrown = Create(
        "SST1480",
        "A constructed exception should be thrown",
        "This '{0}' is created and then discarded; throw it or remove it",
        ExceptionNeverThrownDescription);

    /// <summary>SST1481 — a bitwise operation uses a constant operand that makes it pointless.</summary>
    public static readonly DiagnosticDescriptor RedundantBitwiseOperation = Create(
        "SST1481",
        "Bitwise operations should not use identity operands",
        "The constant operand makes '{0}' pointless; it can only return the value unchanged or discard it entirely",
        RedundantBitwiseOperationDescription);

    /// <summary>SST1482 — GetHashCode reads state that can change.</summary>
    public static readonly DiagnosticDescriptor MutableGetHashCode = Create(
        "SST1482",
        "GetHashCode should not read mutable state",
        "'{0}' can change after the object is hashed, which loses it in any hash-based collection",
        MutableGetHashCodeDescription);

    /// <summary>SST1483 — a constructor calls a member a derived type can override.</summary>
    public static readonly DiagnosticDescriptor VirtualCallInConstructor = Create(
        "SST1483",
        "Constructors should not call overridable members",
        "'{0}' is overridable and is called from a constructor, so a derived override runs before its fields are initialized",
        VirtualCallInConstructorDescription);

    /// <summary>SST1484 — a declaration hides a field or property from an enclosing scope.</summary>
    public static readonly DiagnosticDescriptor ShadowedDeclaration = Create(
        "SST1484",
        "Declarations should not shadow an outer field or property",
        "'{0}' shadows the {1} of the same name; rename it or qualify the outer one",
        ShadowedDeclarationDescription);

    /// <summary>SST1485 — a member that callers assume cannot throw throws.</summary>
    public static readonly DiagnosticDescriptor UnexpectedThrow = Create(
        "SST1485",
        "Members that must not throw should not throw",
        "'{0}' should not throw; callers cannot handle an exception here",
        UnexpectedThrowDescription);

    /// <summary>SST1486 — the same string literal is written repeatedly instead of being named once.</summary>
    public static readonly DiagnosticDescriptor DuplicatedStringLiteral = Create(
        "SST1486",
        "Repeated string literals should be named constants",
        "The literal \"{0}\" appears {1} times in this file; give it a name once",
        DuplicatedStringLiteralDescription);

    /// <summary>SST1487 — a collection element is assigned twice with nothing reading it in between.</summary>
    public static readonly DiagnosticDescriptor OverwrittenCollectionElement = Create(
        "SST1487",
        "Collection elements should not be overwritten before they are read",
        "'{0}' is assigned again with nothing reading it in between, so the first value is lost",
        OverwrittenCollectionElementDescription);

    /// <summary>SST1488 — an exception type does not declare the constructors callers expect.</summary>
    public static readonly DiagnosticDescriptor ExceptionStandardConstructors = Create(
        "SST1488",
        "Exception types should declare the standard constructors",
        "'{0}' does not declare {1}",
        ExceptionStandardConstructorsDescription);

    /// <summary>SST1489 — an exception type carries the formatter-based serialization members, obsolete on this target.</summary>
    public static readonly DiagnosticDescriptor ObsoleteSerializationMember = Create(
        "SST1489",
        "Exception types should not carry formatter-based serialization members",
        "'{0}' is obsolete on this target framework and can be removed",
        ObsoleteSerializationMemberDescription);

    /// <summary>SST1490 — a base list names something the declaration already implies.</summary>
    public static readonly DiagnosticDescriptor RedundantBaseListEntry = Create(
        "SST1490",
        "Base lists should not state what is already implied",
        "'{0}' is already implied by the rest of the base list; remove it",
        RedundantBaseListEntryDescription);

    /// <summary>SST1491 — a modifier restates the declaration's default.</summary>
    public static readonly DiagnosticDescriptor RedundantModifier = Create(
        "SST1491",
        "Modifiers should not restate the default",
        "'{0}' is the default here and can be removed",
        RedundantModifierDescription);

    /// <summary>SST1492 — a value is tested against what it is about to be assigned.</summary>
    public static readonly DiagnosticDescriptor SelfAssignmentGuard = Create(
        "SST1492",
        "Do not test a value against what it is about to be assigned",
        "'{0}' is compared with the value it is then assigned; the guard decides nothing",
        SelfAssignmentGuardDescription);

    /// <summary>SST1493 — a method's body is a single constant.</summary>
    public static readonly DiagnosticDescriptor MethodReturnsConstant = Create(
        "SST1493",
        "Methods should not return a constant",
        "'{0}' always returns the same value; expose it as a constant or a property",
        MethodReturnsConstantDescription);

    /// <summary>SST1494 — an argument repeats a parameter's default value.</summary>
    public static readonly DiagnosticDescriptor RedundantDefaultArgument = Create(
        "SST1494",
        "Do not pass an argument that repeats the parameter's default",
        "'{0}' already defaults to this value; the argument can be omitted",
        RedundantDefaultArgumentDescription);

    /// <summary>SST1495 — reference equality is used on a type that defines value equality.</summary>
    public static readonly DiagnosticDescriptor ReferenceEqualityOnValueEqualType = Create(
        "SST1495",
        "Do not compare with == when the type overrides Equals",
        "'{0}' overrides Equals but does not overload ==; this compares references, not values",
        ReferenceEqualityOnValueEqualTypeDescription);

    /// <summary>SST1496 — an abstract type declares nothing abstract.</summary>
    public static readonly DiagnosticDescriptor AbstractTypeWithoutAbstractMembers = Create(
        "SST1496",
        "Abstract types should declare something abstract",
        "'{0}' is abstract but declares no abstract member; it is a base class, not a contract",
        AbstractTypeWithoutAbstractMembersDescription);

    /// <summary>SST1497 — a local is declared and never read.</summary>
    public static readonly DiagnosticDescriptor UnusedLocal = Create(
        "SST1497",
        "Unused locals should be removed",
        "'{0}' is never read; remove it or use a discard",
        UnusedLocalDescription);

    /// <summary>SST1498 — a private member is used only from a nested type.</summary>
    public static readonly DiagnosticDescriptor PrivateMemberUsedOnlyByNestedType = Create(
        "SST1498",
        "Move a private member used only by a nested type into it",
        "'{0}' is used only by '{1}'; move it there",
        PrivateMemberUsedOnlyByNestedTypeDescription);

    /// <summary>SST1499 — a mutable static field is exposed.</summary>
    public static readonly DiagnosticDescriptor MutableStaticField = Create(
        "SST1499",
        "Do not expose a mutable static field",
        "'{0}' is static, visible and mutable; any caller can change it for every caller",
        MutableStaticFieldDescription);

    /// <summary>The SST1472 rule description.</summary>
    private const string TooManyParametersDescription =
        "A long parameter list is easy to call wrongly — adjacent arguments of the same type are silently swappable — and usually means a "
        + "type is missing. A signature that cannot be changed is excluded: an override, an interface implementation, a P/Invoke, a "
        + "'Deconstruct', and a lambda whose shape its delegate dictates. A positional record is the parameter object this rule asks for, "
        + "so it is excluded by default. Parameters the caller never writes — an extension receiver, a caller-info parameter — are not "
        + "counted. Configure the maximum with 'stylesharp.SST1472.max_parameters'.";

    /// <summary>The SST1468 rule description.</summary>
    private const string UseShortCircuitOperatorDescription =
        "The bitwise '&' and '|' operators on booleans always evaluate both operands; the conditional forms stop as soon as the answer "
        + "is known. Reported only when the right operand has no side effects to skip.";

    /// <summary>The SST1471 rule description.</summary>
    private const string MagicNumberDescription =
        "A numeric literal buried in an expression states a value without stating its meaning. A literal is excluded when it already sits "
        + "at a declaration that names it, when it is -1, 0 or 1, when it is a bit pattern or shift distance, when it guards a Count or "
        + "Length, or when its position already carries the meaning. Configure the allowed values with "
        + "'stylesharp.SST1471.magic_number_allowed_values'.";

    /// <summary>The SST1473 rule description.</summary>
    private const string FloatingPointEqualityDescription =
        "Binary floating-point arithmetic rounds, so two values that are mathematically equal often differ in their last bits and an exact "
        + "'==' silently answers false. Comparing against NaN is worse: every operator except '!=' answers false, including 'x == x'. "
        + "Compare a difference against a tolerance, or use 'double.IsNaN'. A comparison against a literal zero is allowed by default "
        + "because it tests a sign or an initialization rather than an arithmetic result; set "
        + "'stylesharp.SST1473.allow_zero_comparison = false' to report it too. 'decimal' is exact and is never reported.";

    /// <summary>The SST1474 rule description.</summary>
    private const string IdenticalOperandsDescription =
        "An operator whose two operands are the same expression either does nothing ('x & x', 'a || a') or answers a constant "
        + "('x == x', 'x - x'), and is almost always a mistyped operand. Only side-effect-free operands are compared, so 'Next() == Next()' "
        + "stays clean. A self-comparison on a floating-point type is the deliberate NaN idiom and is left to SST1473.";

    /// <summary>The SST1475 rule description.</summary>
    private const string DuplicateConditionDescription =
        "In an if/else-if chain the first matching condition wins, so a condition that repeats an earlier one guards a branch that can "
        + "never execute — the intended condition was almost certainly meant to differ. The same reasoning covers a 'switch' whose labels "
        + "repeat a constant. Two adjacent 'if' statements that test the same condition are reported too: there both branches run, so the "
        + "code is not dead, but either the two should be one 'if' or one of the conditions was meant to say something else. Only "
        + "side-effect-free conditions are compared, and the adjacent pair is only reported when the first 'if' provably cannot change what "
        + "the condition reads.";

    /// <summary>The SST1476 rule description.</summary>
    private const string IdenticalBranchesDescription =
        "When every branch of an 'if'/'else', a conditional expression or a 'switch' does the same thing, the condition is dead weight and "
        + "the code says something it does not mean — usually one branch was meant to differ. Reported only when the chain is exhaustive "
        + "(an 'if' with an 'else', a 'switch' with a 'default'), because a partial chain that falls through to nothing is a different "
        + "shape. Configure the smallest body that counts with 'stylesharp.SST1476.minimum_statements'.";

    /// <summary>The SST1477 rule description.</summary>
    private const string IntegerDivisionAsFloatingPointDescription =
        "Dividing two integers performs integer division and throws the remainder away; widening the truncated result to 'float', 'double' "
        + "or 'decimal' afterwards cannot bring it back, so '1 / 2' stored in a double is 0, not 0.5. Cast one operand before the division "
        + "when a fractional result is meant. A division by a compile-time constant of 1, or one whose operands make the truncation "
        + "irrelevant, is still reported: the widening is what makes the intent ambiguous.";

    /// <summary>The SST1478 rule description.</summary>
    private const string SuspiciousShiftCountDescription =
        "C# masks a shift count to the operand's width, so shifting a 32-bit value by 32 shifts by 0 and returns the value unchanged — not "
        + "zero, as the code reads. A shift by 0 is a no-op that usually means a loop bound or an offset is wrong. Only constant counts are "
        + "reported, so a computed shift stays clean. A count of 0 that is written to make a table of shifts regular can be allowed with "
        + "'stylesharp.SST1478.allow_zero_shift'.";

    /// <summary>The SST1479 rule description.</summary>
    private const string MeaninglessCountComparisonDescription =
        "'Count', 'Length' and the result of 'Enumerable.Count()' are never negative, so 'count >= 0' is always true and 'count < 0' is "
        + "always false — the test does nothing and hides the check that was meant. Comparisons against a negative literal are reported for "
        + "the same reason. The rule reads the well-known count members of arrays, strings, spans and the BCL collection interfaces.";

    /// <summary>The SST1480 rule description.</summary>
    private const string ExceptionNeverThrownDescription =
        "Creating an exception has no effect on control flow — a 'new InvalidOperationException(...)' written as a statement, or as the "
        + "value of an expression nobody consumes, means a 'throw' was forgotten and the error path silently continues. An exception that "
        + "is returned, assigned, passed as an argument or captured is left alone: those are the shapes of an exception factory.";

    /// <summary>The SST1481 rule description.</summary>
    private const string RedundantBitwiseOperationDescription =
        "'x | 0', 'x ^ 0' and 'x & ~0' all return 'x' unchanged, and 'x & 0' always returns 0 whatever 'x' was. The operation reads as "
        + "though it does something, so the constant is usually the wrong one — a mask that was meant to be non-zero, or one whose width is "
        + "off. Only constant operands are examined. A shift by zero is the same kind of mistake but its meaning depends on the operand's "
        + "width, so it belongs to SST1478.";

    /// <summary>The SST1482 rule description.</summary>
    private const string MutableGetHashCodeDescription =
        "A hash-based collection places an object in a bucket when it is added and looks in that bucket when it is asked for. If a field the "
        + "hash reads is then reassigned, the object's hash moves but the object does not, and it can no longer be found — not even by a "
        + "reference to itself. Hash only 'readonly' fields, get-only auto-properties, or values that cannot change for the object's "
        + "lifetime.";

    /// <summary>The SST1483 rule description.</summary>
    private const string VirtualCallInConstructorDescription =
        "A base constructor runs before the derived constructor, so a virtual call made from it dispatches to the derived override while the "
        + "derived fields are still at their defaults — the override sees an object that does not exist yet, and 'readonly' fields it reads "
        + "are null. Seal the type, seal the member, or move the call out of the constructor into an initialization method the caller "
        + "invokes. A call on another instance is not reported, and neither is one in a sealed type, which cannot be overridden.";

    /// <summary>The SST1484 rule description.</summary>
    private const string ShadowedDeclarationDescription =
        "A local or parameter that reuses the name of a field or property makes every unqualified use of that name ambiguous to a reader — "
        + "and an assignment that was meant for the field silently updates the local instead. A constructor parameter that feeds the field "
        + "it shadows is the idiomatic exception and is not reported. A nested type's field or property that reuses the name of a "
        + "containing type's static field or property is reported for the same reason: inside the nested type the simple name resolves to "
        + "the nested member. Set 'stylesharp.SST1484.check_base_types = true' to also report a field that shadows one inherited from a "
        + "base type.";

    /// <summary>The SST1485 rule description.</summary>
    private const string UnexpectedThrowDescription =
        "Some members are invoked implicitly, by the runtime or by code that has no way to catch — 'Equals', 'GetHashCode', 'ToString', "
        + "'Dispose', a static constructor, an equality operator and an implicit conversion. An exception from one of them surfaces far from "
        + "its cause, and from 'Dispose' it can replace the exception already unwinding. Return a sentinel instead of throwing. "
        + "'NotImplementedException' and 'NotSupportedException' are allowed: they mark a member as deliberately absent. Add members with "
        + "'stylesharp.SST1485.additional_members'.";

    /// <summary>The SST1486 rule description.</summary>
    private const string DuplicatedStringLiteralDescription =
        "A string written out several times has to be changed in several places, and a typo in one copy compiles cleanly and fails at "
        + "runtime. Naming it once makes the change atomic and gives the value a meaning. Short literals are excluded — a name for \"\" or "
        + "\",\" buys nothing — as are the places a repeated literal is idiomatic: attribute arguments, 'nameof' operands, constant "
        + "declarations, and the literals a switch already labels. Configure the number of copies allowed with "
        + "'stylesharp.SST1486.duplicate_string_threshold' and the shortest literal that counts with "
        + "'stylesharp.SST1486.minimum_string_length'.";

    /// <summary>The SST1487 rule description.</summary>
    private const string OverwrittenCollectionElementDescription =
        "Assigning the same element twice in a row throws the first value away — the index or key was almost certainly meant to differ, and "
        + "the loop that was supposed to advance it does not. Only a literal repeat is reported: the two assignments must be adjacent, the "
        + "receiver and the index must be the same side-effect-free expression, and nothing between them may read the element or touch the "
        + "collection. A compound assignment ('sum[i] += x') reads before it writes and is never reported.";

    /// <summary>The SST1488 rule description.</summary>
    private const string ExceptionStandardConstructorsDescription =
        "Callers expect to be able to construct an exception the three ways every exception in the framework supports: with no argument, "
        + "with a message, and with a message plus the inner exception that caused it. The last one matters most — an exception type that "
        + "cannot wrap an inner exception forces the code catching it to either lose the original cause or give up on rethrowing at all. "
        + "The constructors are matched on the type they take, not on their parameter names. This rule deliberately does NOT ask for the "
        + "'(SerializationInfo, StreamingContext)' constructor: formatter-based serialization is obsolete on modern .NET, and adding it "
        + "back is a step in the wrong direction — see SST1489. Configure with 'stylesharp.SST1488.require_parameterless' and "
        + "'stylesharp.SST1488.include_non_public_types'.";

    /// <summary>The SST1489 rule description.</summary>
    private const string ObsoleteSerializationMemberDescription =
        "The 'protected T(SerializationInfo, StreamingContext)' constructor and the 'GetObjectData' override exist to support "
        + "BinaryFormatter, which modern .NET has obsoleted and removed: the runtime never calls either member, so they are dead code that "
        + "still has to be read, maintained, and kept in step with every new field. The rule is gated on the target framework actually "
        + "having obsoleted them — it probes whether 'Exception.GetObjectData' carries an [Obsolete] attribute — so a project still "
        + "targeting .NET Framework or netstandard2.0, where the members are live, is never reported.";

    /// <summary>The RedundantBaseListEntry rule description.</summary>
    private const string RedundantBaseListEntryDescription =
        "A base list that names 'object', or an interface a base type or another interface already brings in, states a fact the compiler "
        + "knew anyway. It reads as though it means something — that this type implements the interface *directly* — and hides the entries "
        + "that do carry information.";

    /// <summary>The RedundantModifier rule description.</summary>
    private const string RedundantModifierDescription =
        "A modifier that names what the declaration already is — 'sealed' on a member of a sealed type, 'virtual' that no type can reach, "
        + "'static' on a member of a static class — adds a keyword a reader must check and discard. It also invites the opposite mistake: "
        + "removing the type's 'sealed' silently changes what the member's modifier means.";

    /// <summary>The SelfAssignmentGuard rule description.</summary>
    private const string SelfAssignmentGuardDescription =
        "'if (x != y) { x = y; }' does the same thing as 'x = y' — the guard only skips an assignment that would have changed nothing. "
        + "The shape usually means the condition or the assignment was mistyped, and the fix is to work out which. A property setter with "
        + "side effects behind it is the one case where the guard is deliberate, so a property whose accessor is written by hand is "
        + "excluded.";

    /// <summary>The MethodReturnsConstant rule description.</summary>
    private const string MethodReturnsConstantDescription =
        "A method whose whole body is 'return 42;' promises a computation it does not do. Callers pay a call, readers look for the logic, "
        + "and nobody can tell the value is fixed without opening it. A constant or a get-only property says the same thing and says it at "
        + "the call site. An override, an interface implementation and a virtual member are excluded: their shape is dictated elsewhere, and "
        + "returning a constant is how a derived type answers a question.";

    /// <summary>The RedundantDefaultArgument rule description.</summary>
    private const string RedundantDefaultArgumentDescription =
        "Passing a value that is already the parameter's default adds noise and — worse — freezes it. When the default later changes, every "
        + "call site that spelled the old one out keeps the old behavior, silently, while the calls that omitted it move on. Only a trailing "
        + "argument is reported, because an earlier one cannot be dropped without naming the ones after it.";

    /// <summary>The ReferenceEqualityOnValueEqualType rule description.</summary>
    private const string ReferenceEqualityOnValueEqualTypeDescription =
        "A type that overrides 'Equals' has said what equality means for it — and a type that does not also overload '==' leaves that "
        + "operator comparing references. So 'a == b' and 'a.Equals(b)' answer differently for the same pair, and the one the author "
        + "reached for first is usually the wrong one. Call 'Equals', or overload the operator so both agree.";

    /// <summary>The AbstractTypeWithoutAbstractMembers rule description.</summary>
    private const string AbstractTypeWithoutAbstractMembersDescription =
        "An abstract type with nothing abstract in it cannot be instantiated but asks nothing of its derived types either — it is a base "
        + "class wearing a contract's clothes. Either give it the member the derived types must supply, or drop 'abstract' and seal it. A "
        + "type that inherits an abstract member it does not implement is genuinely abstract and is not reported.";

    /// <summary>The UnusedLocal rule description.</summary>
    private const string UnusedLocalDescription =
        "A local nobody reads is either dead code or a bug — the value it holds was meant to be used. Removing it is free; keeping it costs "
        + "every future reader the time to work out that it does not matter. A local assigned from a call with side effects is still "
        + "reported, but the fix keeps the call and drops only the variable, so the effect survives.";

    /// <summary>The PrivateMemberUsedOnlyByNestedType rule description.</summary>
    private const string PrivateMemberUsedOnlyByNestedTypeDescription =
        "A private member that only a nested type touches is declared further out than it needs to be. Moving it in narrows what a reader "
        + "has to hold in their head when they change it, and it stops the outer type from growing a surface it does not use.";

    /// <summary>The MutableStaticField rule description.</summary>
    private const string MutableStaticFieldDescription =
        "A visible static field that is not 'readonly' is global mutable state: any code in any thread can reassign it, and every other "
        + "user of the type sees the change with no way to notice it happened. A 'readonly' array or list is barely better — the reference "
        + "cannot move but the contents can — so a visible static collection is reported too. Expose a copy, an immutable collection, or a "
        + "property with the mutation you actually mean.";

    /// <summary>Creates a Warning-severity Maintainability descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Maintainability", description);

    /// <summary>Creates a Maintainability descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Maintainability", description);
}
